using System.Net;
using System.Net.Http.Json;
using Bookify.Services.Booking.Api.Endpoints.Properties.GetAvailability;
using Bookify.Services.Booking.Domain.Bookings.Pricing;
using Bookify.Services.Booking.Domain.Shared.ValueObjects;
using Bookify.Services.Booking.Integration.Tests.Infrastructure;

namespace Bookify.Services.Booking.Integration.Tests.Endpoints.Properties;

[Collection(BookingApiTestFixture.Name)]
[Trait("Category", "Integration")]
public sealed class AvailabilityQuoteEndpointTests
{
    private readonly BookingApiFactory _factory;

    public AvailabilityQuoteEndpointTests(BookingApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetAvailability_ShouldReturnCalculatedInformativeQuote()
    {
        // Arrange
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        SeedData data = await BookingDatabaseTestSeeder.SeedPropertyWithRoomAsync(
            _factory,
            propertyName: $"Availability Quote {Guid.NewGuid():N}",
            configureUnit: rentableUnit =>
            {
                var season = PricingSeason.Create(
                    new DateOnly(2026, 12, 25),
                    new DateOnly(2026, 12, 26),
                    Money.Create(200m, "USD").Value,
                    priority: 10).Value;

                rentableUnit.AddPricingSeason(season);
            },
            cancellationToken: cancellationToken);

        HttpClient client = _factory.CreateClient();

        string endpoint = $"/api/v1/properties/{data.PropertyId}/availability?checkInDate=2026-12-24&checkOutDate=2026-12-27&guestCount=3";

        // Act
        using HttpResponseMessage response = await client.GetAsync(endpoint, cancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        GetAvailabilityResponse body = Assert.IsType<GetAvailabilityResponse>(
            await response.Content.ReadFromJsonAsync<GetAvailabilityResponse>(cancellationToken));

        Assert.Equal(data.PropertyId, body.PropertyId);
        Assert.Equal(3, body.NumberOfNights);
        Assert.Equal(3, body.GuestCount);

        AvailableRentableUnitResponse unit = Assert.Single(body.AvailableUnits);
        Assert.Equal(data.RentableUnitId, unit.Id);
        Assert.Equal("Room", unit.Type);
        Assert.Equal(4, unit.MaximumCapacity);
        Assert.False(unit.IsEntireProperty);
        Assert.Equal(440m, unit.Quote.AccommodationPrice);
        Assert.Equal(75m, unit.Quote.ExtraGuestPrice);
        Assert.Equal(515m, unit.Quote.TotalPrice);
        Assert.Equal("USD", unit.Quote.Currency);
    }
}
