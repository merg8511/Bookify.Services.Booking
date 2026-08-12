using Bookify.Services.Booking.Domain.Bookings.Services;
using Bookify.Services.Booking.Domain.Bookings.ValueObjects;
using Bookify.Services.Booking.Domain.Shared.ValueObjects;

namespace Bookify.Services.Booking.Domain.Tests.Bookings.Services;

public sealed class BookingPricingEngineTests
{
    [Fact]
    public void CalculateBasePrice_WithOneNight_ShouldReturnNightlyRate()
    {
        // ARRANGE
        Money nightlyRate =
            Money.Create(
                125m,
                "USD").Value;

        StayPeriod stayPeriod =
            StayPeriod.Create(
                new DateOnly(2026, 9, 10),
                new DateOnly(2026,9,11)).Value;

        // ACT
        var result = BookingPricingEngine.CalculateBasePrice(
            nightlyRate,
            stayPeriod);

        // ASSERT
        Assert.True(result.IsSuccess);

        Assert.Equal(
            125m,
            result.Value.Amount);

        Assert.Equal(
            "USD",
            result.Value.Currency);
    }

    [Fact]
    public void CalculateBasePrice_WithMultipleNights_ShouldMultiplyNightlyRate()
    {
        // ARRANGE
        Money nightlyRate =
            Money.Create(
                150.75m,
                "USD")
            .Value;

        StayPeriod stayPeriod =
            StayPeriod.Create(
                new DateOnly(2026, 9, 10),
                new DateOnly(2026, 9, 13))
            .Value;

        // ACT
        var result =
            BookingPricingEngine.CalculateBasePrice(
                nightlyRate,
                stayPeriod);

        // ASSERT
        Assert.True(result.IsSuccess);

        Assert.Equal(
            452.25m,
            result.Value.Amount);

        Assert.Equal(
            "USD",
            result.Value.Currency);
    }

    [Fact]
    public void CalculateBasePrice_WithNullNightlyRate_ShouldThrow()
    {
        // ARRANGE
        StayPeriod stayPeriod =
            StayPeriod.Create(
                new DateOnly(2026, 9, 10),
                new DateOnly(2026, 9, 13))
            .Value;

        // ACT
        void Action()
        {
            BookingPricingEngine.CalculateBasePrice(
                null!,
                stayPeriod);
        }

        // ASSERT
        Assert.Throws<ArgumentNullException>(Action);
    }

    [Fact]
    public void CalculateBasePrice_WithNullStayPeriod_ShouldThrow()
    {
        // ARRANGE
        Money nightlyRate =
            Money.Create(
                150m,
                "USD")
            .Value;

        // ACT
        void Action()
        {
            BookingPricingEngine.CalculateBasePrice(
                nightlyRate,
                null!);
        }

        // ASSERT
        Assert.Throws<ArgumentNullException>(Action);
    }
}
