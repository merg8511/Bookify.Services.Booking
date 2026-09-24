using Bookify.Services.Booking.Api.Endpoints.Bookings.Create;
using Bookify.Services.Booking.Api.Endpoints.Bookings.Get;
using Bookify.Services.Booking.Api.Endpoints.Properties.GetAvailability;
using Bookify.Services.Booking.Application.Abstractions.Persistence;
using Bookify.Services.Booking.Application.Abstractions.Persistence.Repositories;
using Bookify.Services.Booking.Domain.Properties;
using Bookify.Services.Booking.Domain.Properties.Pricing;
using Bookify.Services.Booking.Domain.Shared.ValueObjects;
using Bookify.Services.Booking.IntegrationTests.Infrastructure;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using System.Data.Common;
using System.Net;
using System.Net.Http.Json;

namespace Bookify.Services.Booking.IntegrationTests.Bookings;

[Collection(BookingApiTestFixture.Name)]
[Trait("Category", "Integration")]
public sealed class BookingQuoteSnapshotIntegrationTests
{
    private readonly BookingApiFactory _factory;

    public BookingQuoteSnapshotIntegrationTests(
        BookingApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task
        CreateBooking_WhenPricingChangesAfterQuote_ShouldRecalculateAndFreezeFinalSnapshot()
    {
        // ARRANGE
        CancellationToken cancellationToken =
            TestContext.Current
                .CancellationToken;

        SeedData data =
            await SeedAsync(
                cancellationToken);

        HttpClient client =
            _factory.CreateClient();

        string availabilityEndpoint =
            $"/api/v1/properties/" +
            $"{data.PropertyId}/availability" +
            "?checkInDate=2026-11-10" +
            "&checkOutDate=2026-11-12" +
            "&guestCount=2";

        // ACT 1
        // Get an informative quote using the initial pricing:
        // 2 nights x 100 USD = 200 USD.
        using HttpResponseMessage
            availabilityResponse =
                await client.GetAsync(
                    availabilityEndpoint,
                    cancellationToken);

        // ASSERT 1
        Assert.Equal(
            HttpStatusCode.OK,
            availabilityResponse.StatusCode);

        GetAvailabilityResponse availability =
            Assert.IsType<
                GetAvailabilityResponse>(
                    await availabilityResponse
                        .Content
                        .ReadFromJsonAsync<
                            GetAvailabilityResponse>(
                                cancellationToken));

        AvailableRentableUnitResponse quotedUnit =
            Assert.Single(
                availability.AvailableUnits);

        Assert.Equal(
            data.RentableUnitId,
            quotedUnit.Id);

        Assert.Equal(
            200m,
            quotedUnit.Quote
                .AccommodationPrice);

        Assert.Equal(
            0m,
            quotedUnit.Quote
                .ExtraGuestPrice);

        Assert.Equal(
            200m,
            quotedUnit.Quote
                .TotalPrice);

        Assert.Equal(
            "USD",
            quotedUnit.Quote
                .Currency);

        decimal informativeQuoteTotal =
            quotedUnit.Quote.TotalPrice;

        // ACT 2
        // Pricing changes after the customer received the quote.
        await UpdatePricingAsync(
            data.RentableUnitId,
            regularNightlyRate: 150m,
            weekendNightlyRate: 150m,
            extraGuestNightlyRate: 25m,
            cancellationToken);

        var createRequest =
            new CreateBookingRequest(
                data.PropertyId,
                data.RentableUnitId,
                new DateOnly(
                    2026,
                    11,
                    10),
                new DateOnly(
                    2026,
                    11,
                    12),
                GuestCount: 2,
                Guest:
                    new CreateBookingGuestRequest(
                        "John Doe",
                        "john@example.com",
                        "+50377778888"));

        using var createMessage =
            new HttpRequestMessage(
                HttpMethod.Post,
                "/api/v1/bookings");

        createMessage.Headers.Add(
            "Idempotency-Key",
            $"quote-snapshot-" +
            $"{Guid.NewGuid():N}");

        createMessage.Content =
            JsonContent.Create(
                createRequest);

        using HttpResponseMessage createResponse =
            await client.SendAsync(
                createMessage,
                cancellationToken);

        // ASSERT 2
        Assert.Equal(
            HttpStatusCode.Created,
            createResponse.StatusCode);

        CreateBookingResponse createdBooking =
            Assert.IsType<
                CreateBookingResponse>(
                    await createResponse.Content
                        .ReadFromJsonAsync<
                            CreateBookingResponse>(
                                cancellationToken));

        Assert.Equal(
            300m,
            createdBooking.Price
                .AccommodationPrice);

        Assert.Equal(
            0m,
            createdBooking.Price
                .ExtraGuestPrice);

        Assert.Equal(
            300m,
            createdBooking.Price
                .TotalPrice);

        Assert.Equal(
            "USD",
            createdBooking.Price
                .Currency);

        Assert.NotEqual(
            informativeQuoteTotal,
            createdBooking.Price
                .TotalPrice);

        Uri bookingLocation =
            Assert.IsType<Uri>(
                createResponse.Headers
                    .Location);

        // ACT 3
        // Change current pricing again after the Booking already exists.
        await UpdatePricingAsync(
            data.RentableUnitId,
            regularNightlyRate: 250m,
            weekendNightlyRate: 250m,
            extraGuestNightlyRate: 25m,
            cancellationToken);

        using HttpResponseMessage
            bookingResponse =
                await client.GetAsync(
                    bookingLocation,
                    cancellationToken);

        // ASSERT 3
        Assert.Equal(
            HttpStatusCode.OK,
            bookingResponse.StatusCode);

        GetBookingResponse persistedBooking =
            Assert.IsType<
                GetBookingResponse>(
                    await bookingResponse.Content
                        .ReadFromJsonAsync<
                            GetBookingResponse>(
                                cancellationToken));

        Assert.Equal(
            createdBooking.Id,
            persistedBooking.Id);

        Assert.Equal(
            createdBooking.BookingReference,
            persistedBooking.BookingReference);

        Assert.NotNull(
            persistedBooking.Price);

        Assert.Equal(
            300m,
            persistedBooking.Price
                .AccommodationPrice);

        Assert.Equal(
            0m,
            persistedBooking.Price
                .ExtraGuestPrice);

        Assert.Equal(
            300m,
            persistedBooking.Price
                .TotalPrice);

        Assert.Equal(
            "USD",
            persistedBooking.Price
                .Currency);

        Assert.Equal(
            createdBooking.Price
                .TotalPrice,
            persistedBooking.Price
                .TotalPrice);

        Assert.NotEqual(
            500m,
            persistedBooking.Price
                .TotalPrice);
    }

    private async Task<SeedData> SeedAsync(
        CancellationToken cancellationToken)
    {
        Property property =
            Property.Create(
                    $"Quote Snapshot Test " +
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
                    $"Room {Guid.NewGuid():N}",
                    RentableUnitType.Room,
                    maximumCapacity: 4,
                    maxBaseGuests: 2)
                .Value;

        rentableUnit.ConfigurePricing(
            RentableUnitPricing.Create(
                    Money.Create(
                            100m,
                            "USD")
                        .Value,
                    Money.Create(
                            100m,
                            "USD")
                        .Value,
                    Money.Create(
                            25m,
                            "USD")
                        .Value)
                .Value);

        using IServiceScope scope =
            _factory.Services
                .CreateScope();

        IPropertyRepository propertyRepository =
            scope.ServiceProvider
                .GetRequiredService<
                    IPropertyRepository>();

        IRentableUnitRepository
            rentableUnitRepository =
                scope.ServiceProvider
                    .GetRequiredService<
                        IRentableUnitRepository>();

        IUnitOfWork unitOfWork =
            scope.ServiceProvider
                .GetRequiredService<
                    IUnitOfWork>();

        propertyRepository.Add(
            property);

        rentableUnitRepository.Add(
            rentableUnit);

        await unitOfWork.SaveChangesAsync(
            cancellationToken);

        return new SeedData(
            property.Id,
            rentableUnit.Id);
    }

    private async Task UpdatePricingAsync(
        Guid rentableUnitId,
        decimal regularNightlyRate,
        decimal weekendNightlyRate,
        decimal extraGuestNightlyRate,
        CancellationToken cancellationToken)
    {
        IDbConnectionFactory connectionFactory =
            _factory.Services
                .GetRequiredService<
                    IDbConnectionFactory>();

        await using DbConnection connection =
            await connectionFactory
                .OpenConnectionAsync(
                    cancellationToken);

        var command =
            new CommandDefinition(
                """
                UPDATE rentable_unit_pricing
                SET
                    regular_nightly_rate_amount =
                        @RegularNightlyRate,

                    weekend_nightly_rate_amount =
                        @WeekendNightlyRate,

                    extra_guest_nightly_rate_amount =
                        @ExtraGuestNightlyRate

                WHERE rentable_unit_id =
                    @RentableUnitId;
                """,
                new
                {
                    RentableUnitId =
                        rentableUnitId,

                    RegularNightlyRate =
                        regularNightlyRate,

                    WeekendNightlyRate =
                        weekendNightlyRate,

                    ExtraGuestNightlyRate =
                        extraGuestNightlyRate
                },
                cancellationToken:
                    cancellationToken);

        int affectedRows =
            await connection.ExecuteAsync(
                command);

        Assert.Equal(
            1,
            affectedRows);
    }

    private sealed record SeedData(
        Guid PropertyId,
        Guid RentableUnitId);
}
