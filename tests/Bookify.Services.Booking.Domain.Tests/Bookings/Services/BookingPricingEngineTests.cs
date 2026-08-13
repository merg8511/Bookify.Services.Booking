using Bookify.Services.Booking.Domain.Bookings.Services;
using Bookify.Services.Booking.Domain.Bookings.ValueObjects;
using Bookify.Services.Booking.Domain.Properties;
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

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void CalculateExtraGuestPrice_WhenGuestCountDoesNotExceedBaseGuests_ShouldReturnZero(
        int guestCountValue)
    {
        // ARRANGE
        Money extraGuestNightlyRate =
            Money.Create(
                25m,
                "USD")
            .Value;

        RentableUnit rentableUnit =
            CreateRentableUnit();

        GuestCount guestCount =
            GuestCount.Create(
                guestCountValue)
            .Value;

        StayPeriod stayPeriod =
            StayPeriod.Create(
                new DateOnly(2026, 9, 10),
                new DateOnly(2026, 9, 13))
            .Value;

        // ACT
        var result =
            BookingPricingEngine.CalculateExtraGuestPrice(
                extraGuestNightlyRate,
                rentableUnit,
                guestCount,
                stayPeriod);

        // ASSERT
        Assert.True(result.IsSuccess);

        Assert.Equal(
            0m,
            result.Value.Amount);

        Assert.Equal(
            "USD",
            result.Value.Currency);
    }

    [Fact]
    public void CalculateExtraGuestPrice_WithOneExtraGuestForOneNight_ShouldReturnNightlySurcharge()
    {
        // ARRANGE
        Money extraGuestNightlyRate =
            Money.Create(
                25m,
                "USD")
            .Value;

        RentableUnit rentableUnit =
            CreateRentableUnit();

        GuestCount guestCount =
            GuestCount.Create(3)
            .Value;

        StayPeriod stayPeriod =
            StayPeriod.Create(
                new DateOnly(2026, 9, 10),
                new DateOnly(2026, 9, 11))
            .Value;

        // ACT
        var result =
            BookingPricingEngine.CalculateExtraGuestPrice(
                extraGuestNightlyRate,
                rentableUnit,
                guestCount,
                stayPeriod);

        // ASSERT
        Assert.True(result.IsSuccess);

        Assert.Equal(
            25m,
            result.Value.Amount);

        Assert.Equal(
            "USD",
            result.Value.Currency);
    }

    [Fact]
    public void CalculateExtraGuestPrice_WithMultipleExtraGuestsAndNights_ShouldMultiplyBoth()
    {
        // ARRANGE
        Money extraGuestNightlyRate =
            Money.Create(
                25m,
                "USD")
            .Value;

        RentableUnit rentableUnit =
            CreateRentableUnit();

        GuestCount guestCount =
            GuestCount.Create(4)
            .Value;

        StayPeriod stayPeriod =
            StayPeriod.Create(
                new DateOnly(2026, 9, 10),
                new DateOnly(2026, 9, 13))
            .Value;

        // ACT
        var result =
            BookingPricingEngine.CalculateExtraGuestPrice(
                extraGuestNightlyRate,
                rentableUnit,
                guestCount,
                stayPeriod);

        // ASSERT
        Assert.True(result.IsSuccess);

        Assert.Equal(
            150m,
            result.Value.Amount);

        Assert.Equal(
            "USD",
            result.Value.Currency);
    }

    private static RentableUnit CreateRentableUnit()
    {
        return RentableUnit.Create(
            Guid.NewGuid(),
            "Habitación principal",
            RentableUnitType.Room,
            maximumCapacity: 5,
            maxBaseGuests: 2)
        .Value;
    }
}
