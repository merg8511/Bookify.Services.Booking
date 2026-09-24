using Bookify.Services.Booking.Domain.Bookings.Pricing;
using Bookify.Services.Booking.Domain.Bookings.Services;
using Bookify.Services.Booking.Domain.Bookings.ValueObjects;
using Bookify.Services.Booking.Domain.Shared;
using Bookify.Services.Booking.Domain.Shared.ValueObjects;

namespace Bookify.Services.Booking.Domain.Tests.Bookings.Services;

public sealed class BookingPricingAvailabilityTests
{
    [Fact]
    public void
        CalculatePrice_WithMaxBaseGuests_ShouldReturnExpectedQuote()
    {
        // Arrange
        Money regularRate =
            Money.Create(
                    100m,
                    "USD")
                .Value;

        Money weekendRate =
            Money.Create(
                    140m,
                    "USD")
                .Value;

        Money extraGuestRate =
            Money.Create(
                    25m,
                    "USD")
                .Value;

        StayPeriod stayPeriod =
            StayPeriod.Create(
                    new DateOnly(
                        2026,
                        12,
                        24),
                    new DateOnly(
                        2026,
                        12,
                        27))
                .Value;

        GuestCount guestCount =
            GuestCount.Create(
                    3)
                .Value;

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

        // Act
        Result<PriceBreakdown> result =
            BookingPricingEngine.CalculatePrice(
                regularRate,
                weekendRate,
                extraGuestRate,
                maxBaseGuests: 2,
                guestCount,
                stayPeriod,
                [season]);

        // Assert
        Assert.True(
            result.IsSuccess);

        Assert.Equal(
            440m,
            result.Value
                .AccommodationPrice
                .Amount);

        Assert.Equal(
            75m,
            result.Value
                .ExtraGuestPrice
                .Amount);

        Assert.Equal(
            515m,
            result.Value
                .TotalPrice
                .Amount);

        Assert.Equal(
            "USD",
            result.Value
                .TotalPrice
                .Currency);
    }
}
