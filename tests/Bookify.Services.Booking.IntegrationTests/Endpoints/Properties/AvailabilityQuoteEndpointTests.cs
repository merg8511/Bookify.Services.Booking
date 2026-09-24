using Bookify.Services.Booking.Api.Endpoints.Properties.GetAvailability;
using Bookify.Services.Booking.Application.Abstractions.Persistence;
using Bookify.Services.Booking.Application.Abstractions.Persistence.Repositories;
using Bookify.Services.Booking.Domain.Bookings.Pricing;
using Bookify.Services.Booking.Domain.Properties;
using Bookify.Services.Booking.Domain.Properties.Pricing;
using Bookify.Services.Booking.Domain.Shared.ValueObjects;
using Bookify.Services.Booking.IntegrationTests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;

namespace Bookify.Services.Booking.IntegrationTests.Endpoints.Properties;

[Collection(
    BookingApiTestFixture.Name)]
[Trait(
    "Category",
    "Integration")]
public sealed class AvailabilityQuoteEndpointTests
{
    private readonly
        BookingApiFactory _factory;

    public AvailabilityQuoteEndpointTests(
        BookingApiFactory factory)
    {
        _factory =
            factory;
    }

    [Fact]
    public async Task
        GetAvailability_ShouldReturnCalculatedInformativeQuote()
    {
        // Arrange
        CancellationToken cancellationToken =
            TestContext.Current
                .CancellationToken;

        SeedData data =
            await SeedAsync(
                cancellationToken);

        HttpClient client =
            _factory.CreateClient();

        string endpoint =
            $"/api/v1/properties/" +
            $"{data.PropertyId}/availability" +
            "?checkInDate=2026-12-24" +
            "&checkOutDate=2026-12-27" +
            "&guestCount=3";

        // Act
        using HttpResponseMessage response =
            await client.GetAsync(
                endpoint,
                cancellationToken);

        // Assert
        Assert.Equal(
            HttpStatusCode.OK,
            response.StatusCode);

        GetAvailabilityResponse body =
            Assert.IsType<
                GetAvailabilityResponse>(
                    await response.Content
                        .ReadFromJsonAsync<
                            GetAvailabilityResponse>(
                                cancellationToken));

        Assert.Equal(
            data.PropertyId,
            body.PropertyId);

        Assert.Equal(
            3,
            body.NumberOfNights);

        Assert.Equal(
            3,
            body.GuestCount);

        AvailableRentableUnitResponse unit =
            Assert.Single(
                body.AvailableUnits);

        Assert.Equal(
            data.RentableUnitId,
            unit.Id);

        Assert.Equal(
            "Room",
            unit.Type);

        Assert.Equal(
            4,
            unit.MaximumCapacity);

        Assert.False(
            unit.IsEntireProperty);

        Assert.Equal(
            440m,
            unit.Quote
                .AccommodationPrice);

        Assert.Equal(
            75m,
            unit.Quote
                .ExtraGuestPrice);

        Assert.Equal(
            515m,
            unit.Quote
                .TotalPrice);

        Assert.Equal(
            "USD",
            unit.Quote
                .Currency);
    }

    private async Task<SeedData> SeedAsync(
        CancellationToken cancellationToken)
    {
        Property property =
            Property.Create(
                    $"Availability Quote " +
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
                            140m,
                            "USD")
                        .Value,
                    Money.Create(
                            25m,
                            "USD")
                        .Value)
                .Value);

        PricingSeason season =
            PricingSeason.Create(
                    new DateOnly(
                        2026,
                        12,
                        25),
                    new DateOnly(
                        2026,
                        12,
                        26),
                    Money.Create(
                            200m,
                            "USD")
                        .Value,
                    priority: 10)
                .Value;

        rentableUnit.AddPricingSeason(
            season);

        using IServiceScope scope =
            _factory.Services
                .CreateScope();

        IPropertyRepository
            propertyRepository =
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

    private sealed record SeedData(
        Guid PropertyId,
        Guid RentableUnitId);
}
