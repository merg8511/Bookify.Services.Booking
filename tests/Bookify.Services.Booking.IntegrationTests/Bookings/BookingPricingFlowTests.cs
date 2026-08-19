using System.Net;
using System.Net.Http.Json;
using Bookify.Services.Booking.Api.Endpoints.Bookings.Create;
using Bookify.Services.Booking.Application.Abstractions.Persistence;
using Bookify.Services.Booking.Application.Abstractions.Persistence.Repositories;
using Bookify.Services.Booking.Domain.Bookings.Pricing;
using Bookify.Services.Booking.Domain.Bookings.Services;
using Bookify.Services.Booking.Domain.Bookings.ValueObjects;
using Bookify.Services.Booking.Domain.Properties;
using Bookify.Services.Booking.Domain.Properties.Pricing;
using Bookify.Services.Booking.Domain.Shared.ValueObjects;
using Bookify.Services.Booking.IntegrationTests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

using DomainBooking = Bookify.Services.Booking.Domain.Bookings.Booking;

namespace Bookify.Services.Booking.IntegrationTests.Bookings;

[Collection(BookingApiTestFixture.Name)]
[Trait("Category", "Integration")]
public sealed class BookingPricingFlowTests
{
    private readonly BookingApiFactory _factory;

    public BookingPricingFlowTests(BookingApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Post_WithCompletePricingConfiguration_ShouldReturnAndPersistExpectedSnapshot()
    {
        // ARRANGE
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        PricingTestData data = await SeedPricingScenarioAsync(cancellationToken);

        var request =
            new CreateBookingRequest(
                data.PropertyId,
                data.RentableUnitId,
                CheckInDate(),
                CheckOutDate(),
                GuestCount: 4);

        // ACT
        HttpResponseMessage response =
            await PostBookingAsync(
                request,
                cancellationToken);

        // ASSERT - HTTP
        Assert.Equal(
            HttpStatusCode.Created,
            response.StatusCode);

        CreateBookingResponse? body =
            await response.Content
                .ReadFromJsonAsync<
                    CreateBookingResponse>(
                        cancellationToken);

        Assert.NotNull(body);

        Assert.NotEqual(
            Guid.Empty,
            body.Id);

        Assert.Equal(
            "PendingApproval",
            body.Status);

        Assert.NotNull(body.Price);

        Assert.Equal(
            780m,
            body.Price.AccommodationPrice);

        Assert.Equal(
            200m,
            body.Price.ExtraGuestPrice);

        Assert.Equal(
            980m,
            body.Price.TotalPrice);

        Assert.Equal(
            "USD",
            body.Price.Currency);

        // ASSERT - POSTGRESQL
        DomainBooking? persistedBooking;

        using (IServiceScope assertionScope =
                _factory.Services.CreateScope())
        {
            IBookingRepository bookingRepository =
                assertionScope
                    .ServiceProvider
                    .GetRequiredService<
                        IBookingRepository>();

            persistedBooking =
                await bookingRepository
                    .GetByIdAsync(
                        body.Id,
                        cancellationToken);
        }

        Assert.NotNull(persistedBooking);

        Assert.NotNull(persistedBooking.PriceSnapshot);

        Assert.Equal(
            780m,
            persistedBooking
                .PriceSnapshot
                .AccommodationPrice
                .Amount);

        Assert.Equal(
            200m,
            persistedBooking
                .PriceSnapshot
                .ExtraGuestPrice
                .Amount);

        Assert.Equal(
            980m,
            persistedBooking
                .PriceSnapshot
                .TotalPrice
                .Amount);

        Assert.Equal(
            "USD",
            persistedBooking
                .PriceSnapshot
                .TotalPrice
                .Currency);

        Assert.Equal(
            body.Price.TotalPrice,
            persistedBooking
                .PriceSnapshot
                .TotalPrice
                .Amount);

        Assert.Equal(
            body.Price.Currency,
            persistedBooking
                .PriceSnapshot
                .TotalPrice
                .Currency);
    }

    [Fact]
    public async Task PricingChangesAfterBookingCreation_ShouldNotChangePersistedPriceSnapshot()
    {
        // ARRANGE
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        PricingTestData data =
            await SeedPricingScenarioAsync(
                cancellationToken);

        var request =
            new CreateBookingRequest(
                data.PropertyId,
                data.RentableUnitId,
                CheckInDate(),
                CheckOutDate(),
                GuestCount: 4);

        HttpResponseMessage response =
            await PostBookingAsync(
                request,
                cancellationToken);

        Assert.Equal(
            HttpStatusCode.Created,
            response.StatusCode);

        CreateBookingResponse? originalResponse =
            await response.Content
                .ReadFromJsonAsync<
                    CreateBookingResponse>(
                        cancellationToken);

        Assert.NotNull(originalResponse);

        Assert.Equal(
            980m,
            originalResponse.Price.TotalPrice);

        // ACT - change the CURRENT pricing configuration.
        using (
            IServiceScope updateScope =
                _factory.Services.CreateScope())
        {
            IRentableUnitRepository rentableUnitRepository =
                updateScope
                    .ServiceProvider
                    .GetRequiredService<
                        IRentableUnitRepository>();

            IUnitOfWork unitOfWork =
                updateScope
                    .ServiceProvider
                    .GetRequiredService<
                        IUnitOfWork>();

            RentableUnit? rentableUnit =
                await rentableUnitRepository
                    .GetByIdAsync(
                        data.RentableUnitId,
                        cancellationToken);

            Assert.NotNull(rentableUnit);

            RentableUnitPricing updatedPricing =
                RentableUnitPricing.Create(
                    Money.Create(
                        400m,
                        "USD")
                    .Value,
                    Money.Create(
                        500m,
                        "USD")
                    .Value,
                    Money.Create(
                        100m,
                        "USD")
                    .Value)
                .Value;

            rentableUnit.ConfigurePricing(updatedPricing);

            PricingSeason newSeason =
                PricingSeason.Create(
                    CheckInDate(),
                    CheckOutDate(),
                    Money.Create(
                        300m,
                        "USD")
                    .Value,
                    priority: 100)
                .Value;

            rentableUnit.AddPricingSeason(
                newSeason);

            await unitOfWork.SaveChangesAsync(
                cancellationToken);
        }

        // Prove that the CURRENT configuration now
        // produces a completely different price.
        using (
            IServiceScope pricingScope =
                _factory.Services.CreateScope())
        {
            IRentableUnitRepository rentableUnitRepository =
                pricingScope
                    .ServiceProvider
                    .GetRequiredService<
                        IRentableUnitRepository>();

            RentableUnit? updatedRentableUnit =
                await rentableUnitRepository
                    .GetByIdAsync(
                        data.RentableUnitId,
                        cancellationToken);

            Assert.NotNull(updatedRentableUnit);

            Assert.NotNull(updatedRentableUnit.Pricing);

            StayPeriod stayPeriod =
                StayPeriod.Create(
                    CheckInDate(),
                    CheckOutDate())
                .Value;

            GuestCount guestCount = GuestCount.Create(4).Value;

            var recalculatedPrice =
                BookingPricingEngine.CalculatePrice(
                    updatedRentableUnit
                        .Pricing
                        .RegularNightlyRate,
                    updatedRentableUnit
                        .Pricing
                        .WeekendNightlyRate,
                    updatedRentableUnit
                        .Pricing
                        .ExtraGuestNightlyRate,
                    updatedRentableUnit,
                    guestCount,
                    stayPeriod,
                    updatedRentableUnit
                        .PricingSeasons);

            Assert.True(recalculatedPrice.IsSuccess);

            Assert.Equal(
                1200m,
                recalculatedPrice
                    .Value
                    .AccommodationPrice
                    .Amount);

            Assert.Equal(
                800m,
                recalculatedPrice
                    .Value
                    .ExtraGuestPrice
                    .Amount);

            Assert.Equal(
                2000m,
                recalculatedPrice
                    .Value
                    .TotalPrice
                    .Amount);

            Assert.NotEqual(
                originalResponse
                    .Price
                    .TotalPrice,
                recalculatedPrice
                    .Value
                    .TotalPrice
                    .Amount);
        }

        // ASSERT - the original Booking must still
        // contain its original frozen price.
        DomainBooking? persistedBooking;

        using (
            IServiceScope bookingScope =
                _factory.Services.CreateScope())
        {
            IBookingRepository bookingRepository =
                bookingScope
                    .ServiceProvider
                    .GetRequiredService<
                        IBookingRepository>();

            persistedBooking =
                await bookingRepository
                    .GetByIdAsync(
                        originalResponse.Id,
                        cancellationToken);
        }

        Assert.NotNull(persistedBooking);

        Assert.NotNull(persistedBooking.PriceSnapshot);

        Assert.Equal(
            780m,
            persistedBooking
                .PriceSnapshot
                .AccommodationPrice
                .Amount);

        Assert.Equal(
            200m,
            persistedBooking
                .PriceSnapshot
                .ExtraGuestPrice
                .Amount);

        Assert.Equal(
            980m,
            persistedBooking
                .PriceSnapshot
                .TotalPrice
                .Amount);

        Assert.Equal(
            "USD",
            persistedBooking
                .PriceSnapshot
                .TotalPrice
                .Currency);

        Assert.Equal(
            originalResponse
                .Price
                .TotalPrice,
            persistedBooking
                .PriceSnapshot
                .TotalPrice
                .Amount);
    }

    private async Task<PricingTestData>
        SeedPricingScenarioAsync(CancellationToken cancellationToken)
    {
        Property property =
            Property.Create(
                $"Pricing Flow Test " +
                $"{Guid.NewGuid():N}",
                "America/El_Salvador",
                new TimeOnly(
                    15,
                    0),
                new TimeOnly(
                    11,
                    0))
            .Value;

        RentableUnit rentableUnit =
            RentableUnit.Create(
                property.Id,
                "Room A",
                RentableUnitType.Room,
                maximumCapacity: 5,
                maxBaseGuests: 2)
            .Value;

        RentableUnitPricing pricing =
            RentableUnitPricing.Create(
                Money.Create(
                    100m,
                    "USD")
                .Value,
                Money.Create(
                    140m,
                    "USD")
                .Value,
                Money.Create(
                    25m,
                    "USD")
                .Value)
            .Value;

        rentableUnit.ConfigurePricing(pricing);

        PricingSeason highSeason =
            PricingSeason.Create(
                new DateOnly(
                    2026,
                    12,
                    25),
                new DateOnly(
                    2026,
                    12,
                    28),
                Money.Create(
                    180m,
                    "USD")
                .Value,
                priority: 10)
            .Value;

        PricingSeason christmas =
            PricingSeason.Create(
                new DateOnly(
                    2026,
                    12,
                    25),
                new DateOnly(
                    2026,
                    12,
                    27),
                Money.Create(
                    250m,
                    "USD")
                .Value,
                priority: 20)
            .Value;

        rentableUnit.AddPricingSeason(highSeason);

        rentableUnit.AddPricingSeason(christmas);

        using IServiceScope seedScope =
            _factory.Services.CreateScope();

        IPropertyRepository propertyRepository =
            seedScope
                .ServiceProvider
                .GetRequiredService<
                    IPropertyRepository>();

        IRentableUnitRepository rentableUnitRepository =
            seedScope
                .ServiceProvider
                .GetRequiredService<
                    IRentableUnitRepository>();

        IUnitOfWork unitOfWork =
            seedScope
                .ServiceProvider
                .GetRequiredService<
                    IUnitOfWork>();

        propertyRepository.Add(property);

        rentableUnitRepository.Add(rentableUnit);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new PricingTestData(
            property.Id,
            rentableUnit.Id);
    }

    private async Task<HttpResponseMessage>
        PostBookingAsync(
            CreateBookingRequest request,
            CancellationToken cancellationToken)
    {
        HttpClient client = _factory.CreateClient();

        using var message =
            new HttpRequestMessage(
                HttpMethod.Post,
                "/api/v1/bookings");

        message.Headers.Add(
            "Idempotency-Key",
            Guid.NewGuid()
                .ToString("N"));

        message.Content = JsonContent.Create(request);

        return await client.SendAsync(message, cancellationToken);
    }

    private static DateOnly CheckInDate()
    {
        return new DateOnly(
            2026,
            12,
            24);
    }

    private static DateOnly CheckOutDate()
    {
        return new DateOnly(
            2026,
            12,
            28);
    }

    private sealed record PricingTestData(
        Guid PropertyId,
        Guid RentableUnitId);
}
