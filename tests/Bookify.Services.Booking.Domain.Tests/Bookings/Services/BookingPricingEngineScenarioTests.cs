using Bookify.Services.Booking.Domain.Bookings.Errors;
using Bookify.Services.Booking.Domain.Bookings.Pricing;
using Bookify.Services.Booking.Domain.Bookings.Services;
using Bookify.Services.Booking.Domain.Bookings.ValueObjects;
using Bookify.Services.Booking.Domain.Properties;
using Bookify.Services.Booking.Domain.Shared.Errors;
using Bookify.Services.Booking.Domain.Shared.ValueObjects;

namespace Bookify.Services.Booking.Domain.Tests.Bookings.Services;

public sealed class BookingPricingEngineScenarioTests
{
    [Fact]
    public void CalculatePrice_WithRegularWeekendSeasonAndExtraGuests_ShouldReturnExpectedBreakdown()
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
            CreateSeason(
                new DateOnly(2026, 12, 25),
                new DateOnly(2026, 12, 27),
                nightlyRate: 250m,
                priority: 20);

        RentableUnit rentableUnit =
            CreateRentableUnit();

        GuestCount guestCount =
            GuestCount.Create(4)
            .Value;

        StayPeriod stayPeriod =
            StayPeriod.Create(
                new DateOnly(2026, 12, 24),
                new DateOnly(2026, 12, 28))
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
            700m,
            result.Value.AccommodationPrice.Amount);

        Assert.Equal(
            200m,
            result.Value.ExtraGuestPrice.Amount);

        Assert.Equal(
            900m,
            result.Value.TotalPrice.Amount);

        Assert.Equal(
            "USD",
            result.Value.TotalPrice.Currency);
    }

    [Fact]
    public void CalculatePrice_WhenGuestCountEqualsMaxBaseGuests_ShouldNotChargeExtraGuests()
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
            GuestCount.Create(2)
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
            0m,
            result.Value.ExtraGuestPrice.Amount);

        Assert.Equal(
            200m,
            result.Value.TotalPrice.Amount);
    }

    [Fact]
    public void CalculatePrice_WithOverlappingSeasons_ShouldUseHighestPriorityForEachNight()
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

        PricingSeason highSeason =
            CreateSeason(
                new DateOnly(2026, 12, 25),
                new DateOnly(2026, 12, 28),
                nightlyRate: 180m,
                priority: 10);

        PricingSeason christmas =
            CreateSeason(
                new DateOnly(2026, 12, 25),
                new DateOnly(2026, 12, 27),
                nightlyRate: 250m,
                priority: 20);

        RentableUnit rentableUnit =
            CreateRentableUnit();

        GuestCount guestCount =
            GuestCount.Create(2)
            .Value;

        StayPeriod stayPeriod =
            StayPeriod.Create(
                new DateOnly(2026, 12, 24),
                new DateOnly(2026, 12, 28))
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
                [
                    highSeason,
                    christmas
                ]);

        // ASSERT
        Assert.True(result.IsSuccess);

        Assert.Equal(
            780m,
            result.Value.AccommodationPrice.Amount);

        Assert.Equal(
            0m,
            result.Value.ExtraGuestPrice.Amount);

        Assert.Equal(
            780m,
            result.Value.TotalPrice.Amount);
    }

    [Fact]
    public void CalculatePrice_WithAmbiguousHighestSeasonPriority_ShouldPropagateFailure()
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

        PricingSeason firstSeason =
            CreateSeason(
                new DateOnly(2026, 12, 20),
                new DateOnly(2026, 12, 30),
                nightlyRate: 200m,
                priority: 20);

        PricingSeason secondSeason =
            CreateSeason(
                new DateOnly(2026, 12, 24),
                new DateOnly(2027, 1, 2),
                nightlyRate: 250m,
                priority: 20);

        RentableUnit rentableUnit =
            CreateRentableUnit();

        GuestCount guestCount =
            GuestCount.Create(2)
            .Value;

        var night =
            new DateOnly(
                2026,
                12,
                25);

        StayPeriod stayPeriod =
            StayPeriod.Create(
                night,
                night.AddDays(1))
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
                [
                    firstSeason,
                    secondSeason
                ]);

        // ASSERT
        Assert.True(result.IsFailure);

        Assert.Equal(
            PricingSeasonErrors.AmbiguousPriority(
                night,
                20),
            result.Error);
    }

    [Fact]
    public void CalculatePrice_WhenAccommodationUsesDifferentCurrencies_ShouldReturnFailure()
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
                "EUR")
            .Value;

        Money extraGuestNightlyRate =
            Money.Create(
                25m,
                "USD")
            .Value;

        RentableUnit rentableUnit =
            CreateRentableUnit();

        GuestCount guestCount =
            GuestCount.Create(2)
            .Value;

        StayPeriod stayPeriod =
            StayPeriod.Create(
                new DateOnly(2026, 9, 11),
                new DateOnly(2026, 9, 12))
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
        Assert.True(result.IsFailure);

        Assert.Equal(
            MoneyErrors.CurrencyMismatch(
                "USD",
                "EUR"),
            result.Error);
    }

    [Fact]
    public void CalculatePrice_WhenExtraGuestPriceUsesDifferentCurrency_ShouldReturnFailure()
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
                "EUR")
            .Value;

        RentableUnit rentableUnit =
            CreateRentableUnit();

        GuestCount guestCount =
            GuestCount.Create(3)
            .Value;

        StayPeriod stayPeriod =
            StayPeriod.Create(
                new DateOnly(2026, 9, 14),
                new DateOnly(2026, 9, 15))
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
        Assert.True(result.IsFailure);

        Assert.Equal(
            MoneyErrors.CurrencyMismatch(
                "USD",
                "EUR"),
            result.Error);
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

    private static PricingSeason CreateSeason(
        DateOnly startDate,
        DateOnly endDate,
        decimal nightlyRate,
        int priority)
    {
        return PricingSeason.Create(
            startDate,
            endDate,
            Money.Create(
                nightlyRate,
                "USD")
            .Value,
            priority)
        .Value;
    }
}
