using Bookify.Services.Booking.Domain.Bookings.Pricing;
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
                new DateOnly(2026, 9, 11)).Value;

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

    [Fact]
    public void CalculateAccommodationPrice_WithOnlyRegularNights_ShouldUseRegularRate()
    {
        // ARRANGE
        Money regularNightlyRate =
            Money.Create(
                100m,
                "USD")
            .Value;

        Money weekendNightlyRate =
            Money.Create(
                140m,
                "USD")
            .Value;

        StayPeriod stayPeriod =
            StayPeriod.Create(
                new DateOnly(2026, 9, 14),
                new DateOnly(2026, 9, 16))
            .Value;

        // ACT
        var result =
            BookingPricingEngine.CalculateAccommodationPrice(
                regularNightlyRate,
                weekendNightlyRate,
                stayPeriod);

        // ASSERT
        Assert.True(result.IsSuccess);

        Assert.Equal(
            200m,
            result.Value.Amount);

        Assert.Equal(
            "USD",
            result.Value.Currency);
    }

    [Fact]
    public void CalculateAccommodationPrice_WithOnlyWeekendNights_ShouldUseWeekendRate()
    {
        // ARRANGE
        Money regularNightlyRate =
            Money.Create(
                100m,
                "USD")
            .Value;

        Money weekendNightlyRate =
            Money.Create(
                140m,
                "USD")
            .Value;

        StayPeriod stayPeriod =
            StayPeriod.Create(
                new DateOnly(2026, 9, 11),
                new DateOnly(2026, 9, 13))
            .Value;

        // ACT
        var result =
            BookingPricingEngine.CalculateAccommodationPrice(
                regularNightlyRate,
                weekendNightlyRate,
                stayPeriod);

        // ASSERT
        Assert.True(result.IsSuccess);

        Assert.Equal(
            280m,
            result.Value.Amount);

        Assert.Equal(
            "USD",
            result.Value.Currency);
    }

    [Fact]
    public void CalculateAccommodationPrice_WithMixedNights_ShouldUseRateForEachNight()
    {
        // ARRANGE
        Money regularNightlyRate =
            Money.Create(
                100m,
                "USD")
            .Value;

        Money weekendNightlyRate =
            Money.Create(
                140m,
                "USD")
            .Value;

        StayPeriod stayPeriod =
            StayPeriod.Create(
                new DateOnly(2026, 9, 10),
                new DateOnly(2026, 9, 14))
            .Value;

        // ACT
        var result =
            BookingPricingEngine.CalculateAccommodationPrice(
                regularNightlyRate,
                weekendNightlyRate,
                stayPeriod);

        // ASSERT
        Assert.True(result.IsSuccess);

        Assert.Equal(
            480m,
            result.Value.Amount);

        Assert.Equal(
            "USD",
            result.Value.Currency);
    }

    [Fact]
    public void CalculateAccommodationPrice_ShouldNotChargeCheckOutDate()
    {
        // ARRANGE
        Money regularNightlyRate =
            Money.Create(
                100m,
                "USD")
            .Value;

        Money weekendNightlyRate =
            Money.Create(
                140m,
                "USD")
            .Value;

        StayPeriod stayPeriod =
            StayPeriod.Create(
                new DateOnly(2026, 9, 10),
                new DateOnly(2026, 9, 11))
            .Value;

        // ACT
        var result =
            BookingPricingEngine.CalculateAccommodationPrice(
                regularNightlyRate,
                weekendNightlyRate,
                stayPeriod);

        // ASSERT
        Assert.True(result.IsSuccess);

        Assert.Equal(
            100m,
            result.Value.Amount);
    }

    [Fact]
    public void CalculateAccommodationPrice_WhenSeasonApplies_ShouldOverrideRegularRate()
    {
        // ARRANGE
        Money regularNightlyRate =
            Money.Create(
                100m,
                "USD")
            .Value;

        Money weekendNightlyRate =
            Money.Create(
                140m,
                "USD")
            .Value;

        PricingSeason season =
            PricingSeason.Create(
                new DateOnly(2026, 12, 24),
                new DateOnly(2026, 12, 28),
                Money.Create(
                    220m,
                    "USD")
                .Value,
                priority: 10)
            .Value;

        StayPeriod stayPeriod =
            StayPeriod.Create(
                new DateOnly(2026, 12, 24),
                new DateOnly(2026, 12, 25))
            .Value;

        // ACT
        var result =
            BookingPricingEngine.CalculateAccommodationPrice(
                regularNightlyRate,
                weekendNightlyRate,
                stayPeriod,
                [season]);

        // ASSERT
        Assert.True(result.IsSuccess);

        Assert.Equal(
            220m,
            result.Value.Amount);
    }

    [Fact]
    public void CalculateAccommodationPrice_WhenSeasonAppliesOnWeekend_ShouldOverrideWeekendRate()
    {
        // ARRANGE
        Money regularNightlyRate =
            Money.Create(
                100m,
                "USD")
            .Value;

        Money weekendNightlyRate =
            Money.Create(
                140m,
                "USD")
            .Value;

        PricingSeason christmas =
            PricingSeason.Create(
                new DateOnly(2026, 12, 24),
                new DateOnly(2026, 12, 27),
                Money.Create(
                    250m,
                    "USD")
                .Value,
                priority: 20)
            .Value;

        StayPeriod stayPeriod =
            StayPeriod.Create(
                new DateOnly(2026, 12, 25),
                new DateOnly(2026, 12, 26))
            .Value;

        // ACT
        var result =
            BookingPricingEngine.CalculateAccommodationPrice(
                regularNightlyRate,
                weekendNightlyRate,
                stayPeriod,
                [christmas]);

        // ASSERT
        Assert.True(result.IsSuccess);

        Assert.Equal(
            250m,
            result.Value.Amount);
    }

    [Fact]
    public void CalculateAccommodationPrice_WithMixedRegularWeekendAndSeasonNights_ShouldResolveEachNight()
    {
        // ARRANGE
        Money regularNightlyRate =
            Money.Create(
                100m,
                "USD")
            .Value;

        Money weekendNightlyRate =
            Money.Create(
                140m,
                "USD")
            .Value;

        PricingSeason christmas =
            PricingSeason.Create(
                new DateOnly(2026, 12, 25),
                new DateOnly(2026, 12, 27),
                Money.Create(
                    250m,
                    "USD")
                .Value,
                priority: 20)
            .Value;

        StayPeriod stayPeriod =
            StayPeriod.Create(
                new DateOnly(2026, 12, 24),
                new DateOnly(2026, 12, 28))
            .Value;

        // ACT
        var result =
            BookingPricingEngine.CalculateAccommodationPrice(
                regularNightlyRate,
                weekendNightlyRate,
                stayPeriod,
                [christmas]);

        // ASSERT
        Assert.True(result.IsSuccess);

        Assert.Equal(
            700m,
            result.Value.Amount);
    }

    [Fact]
    public void CalculatePrice_WithAccommodationAndExtraGuests_ShouldReturnBreakdown()
    {
        // ARRANGE
        Money regularNightlyRate =
            Money.Create(
                100m,
                "USD")
            .Value;

        Money weekendNightlyRate =
            Money.Create(
                140m,
                "USD")
            .Value;

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
                new DateOnly(2026, 9, 14),
                new DateOnly(2026, 9, 16))
            .Value;

        // ACT
        var result =
            BookingPricingEngine.CalculatePrice(
                regularNightlyRate,
                weekendNightlyRate,
                extraGuestNightlyRate,
                rentableUnit,
                guestCount,
                stayPeriod);

        // ASSERT
        Assert.True(result.IsSuccess);

        Assert.Equal(
            200m,
            result.Value.AccommodationPrice.Amount);

        Assert.Equal(
            100m,
            result.Value.ExtraGuestPrice.Amount);

        Assert.Equal(
            300m,
            result.Value.TotalPrice.Amount);

        Assert.Equal(
            "USD",
            result.Value.TotalPrice.Currency);
    }

    [Fact]
    public void CalculatePrice_WhenSeasonApplies_ShouldUseSeasonRateInBreakdown()
    {
        // ARRANGE
        Money regularNightlyRate =
            Money.Create(
                100m,
                "USD")
            .Value;

        Money weekendNightlyRate =
            Money.Create(
                140m,
                "USD")
            .Value;

        Money extraGuestNightlyRate =
            Money.Create(
                25m,
                "USD")
            .Value;

        PricingSeason christmas =
            PricingSeason.Create(
                new DateOnly(2026, 12, 24),
                new DateOnly(2026, 12, 27),
                Money.Create(
                    250m,
                    "USD")
                .Value,
                priority: 20)
            .Value;

        RentableUnit rentableUnit =
            CreateRentableUnit();

        GuestCount guestCount =
            GuestCount.Create(3)
            .Value;

        StayPeriod stayPeriod =
            StayPeriod.Create(
                new DateOnly(2026, 12, 25),
                new DateOnly(2026, 12, 26))
            .Value;

        // ACT
        var result =
            BookingPricingEngine.CalculatePrice(
                regularNightlyRate,
                weekendNightlyRate,
                extraGuestNightlyRate,
                rentableUnit,
                guestCount,
                stayPeriod,
                [christmas]);

        // ASSERT
        Assert.True(result.IsSuccess);

        Assert.Equal(
            250m,
            result.Value.AccommodationPrice.Amount);

        Assert.Equal(
            25m,
            result.Value.ExtraGuestPrice.Amount);

        Assert.Equal(
            275m,
            result.Value.TotalPrice.Amount);
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
