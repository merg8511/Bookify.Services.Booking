using Bookify.Services.Booking.Domain.Bookings.Errors;
using Bookify.Services.Booking.Domain.Bookings.Pricing;
using Bookify.Services.Booking.Domain.Bookings.Services;
using Bookify.Services.Booking.Domain.Bookings.ValueObjects;
using Bookify.Services.Booking.Domain.Properties.Pricing;
using Bookify.Services.Booking.Domain.Shared;
using Bookify.Services.Booking.Domain.Shared.Errors;
using Bookify.Services.Booking.Domain.Shared.ValueObjects;

namespace Bookify.Services.Booking.Domain.Tests.Bookings.Services;

public sealed class BookingPricingEngineTests
{
    [Fact]
    public void CalculatePrice_WithoutSeasons_ShouldUseRegularAndWeekendRates()
    {
        // ARRANGE
        RentableUnitPricing pricing =
            CreatePricing();

        StayPeriod stayPeriod =
            StayPeriod.Create(
                    new DateOnly(
                        2026,
                        9,
                        10),
                    new DateOnly(
                        2026,
                        9,
                        14))
                .Value;

        GuestCount guestCount =
            GuestCount.Create(2)
                .Value;

        // ACT
        Result<PriceBreakdown> result =
            BookingPricingEngine.CalculatePrice(
                pricing,
                maxBaseGuests: 2,
                guestCount,
                stayPeriod,
                []);

        // ASSERT
        Assert.True(
            result.IsSuccess);

        Assert.Equal(
            480m,
            result.Value
                .AccommodationPrice
                .Amount);

        Assert.Equal(
            0m,
            result.Value
                .ExtraGuestPrice
                .Amount);

        Assert.Equal(
            480m,
            result.Value
                .TotalPrice
                .Amount);

        Assert.Equal(
            "USD",
            result.Value
                .TotalPrice
                .Currency);
    }

    [Fact]
    public void CalculatePrice_WithRegularWeekendSeasonAndExtraGuests_ShouldReturnExpectedBreakdown()
    {
        // ARRANGE
        RentableUnitPricing pricing =
            CreatePricing();

        PricingSeason christmas =
            CreateSeason(
                new DateOnly(
                    2026,
                    12,
                    25),
                new DateOnly(
                    2026,
                    12,
                    27),
                nightlyRate: 250m,
                currency: "USD",
                priority: 20);

        GuestCount guestCount =
            GuestCount.Create(4)
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
                        28))
                .Value;

        // ACT
        Result<PriceBreakdown> result =
            BookingPricingEngine.CalculatePrice(
                pricing,
                maxBaseGuests: 2,
                guestCount,
                stayPeriod,
                [christmas]);

        // ASSERT
        Assert.True(
            result.IsSuccess);

        Assert.Equal(
            700m,
            result.Value
                .AccommodationPrice
                .Amount);

        Assert.Equal(
            200m,
            result.Value
                .ExtraGuestPrice
                .Amount);

        Assert.Equal(
            900m,
            result.Value
                .TotalPrice
                .Amount);

        Assert.Equal(
            "USD",
            result.Value
                .TotalPrice
                .Currency);
    }

    [Fact]
    public void CalculatePrice_WhenGuestCountEqualsMaxBaseGuests_ShouldNotChargeExtraGuests()
    {
        // ARRANGE
        RentableUnitPricing pricing =
            CreatePricing();

        GuestCount guestCount =
            GuestCount.Create(2)
                .Value;

        StayPeriod stayPeriod =
            StayPeriod.Create(
                    new DateOnly(
                        2026,
                        9,
                        14),
                    new DateOnly(
                        2026,
                        9,
                        16))
                .Value;

        // ACT
        Result<PriceBreakdown> result =
            BookingPricingEngine.CalculatePrice(
                pricing,
                maxBaseGuests: 2,
                guestCount,
                stayPeriod,
                []);

        // ASSERT
        Assert.True(
            result.IsSuccess);

        Assert.Equal(
            200m,
            result.Value
                .AccommodationPrice
                .Amount);

        Assert.Equal(
            0m,
            result.Value
                .ExtraGuestPrice
                .Amount);

        Assert.Equal(
            200m,
            result.Value
                .TotalPrice
                .Amount);
    }

    [Fact]
    public void CalculatePrice_WithOverlappingSeasons_ShouldUseHighestPriorityForEachNight()
    {
        // ARRANGE
        RentableUnitPricing pricing =
            CreatePricing();

        PricingSeason highSeason =
            CreateSeason(
                new DateOnly(
                    2026,
                    12,
                    25),
                new DateOnly(
                    2026,
                    12,
                    28),
                nightlyRate: 180m,
                currency: "USD",
                priority: 10);

        PricingSeason christmas =
            CreateSeason(
                new DateOnly(
                    2026,
                    12,
                    25),
                new DateOnly(
                    2026,
                    12,
                    27),
                nightlyRate: 250m,
                currency: "USD",
                priority: 20);

        GuestCount guestCount =
            GuestCount.Create(2)
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
                        28))
                .Value;

        // ACT
        Result<PriceBreakdown> result =
            BookingPricingEngine.CalculatePrice(
                pricing,
                maxBaseGuests: 2,
                guestCount,
                stayPeriod,
                [
                    highSeason,
                    christmas
                ]);

        // ASSERT
        Assert.True(
            result.IsSuccess);

        Assert.Equal(
            780m,
            result.Value
                .AccommodationPrice
                .Amount);

        Assert.Equal(
            780m,
            result.Value
                .TotalPrice
                .Amount);
    }

    [Fact]
    public void CalculatePrice_WithAmbiguousHighestSeasonPriority_ShouldReturnFailure()
    {
        // ARRANGE
        RentableUnitPricing pricing =
            CreatePricing();

        PricingSeason firstSeason =
            CreateSeason(
                new DateOnly(
                    2026,
                    12,
                    20),
                new DateOnly(
                    2026,
                    12,
                    30),
                nightlyRate: 200m,
                currency: "USD",
                priority: 20);

        PricingSeason secondSeason =
            CreateSeason(
                new DateOnly(
                    2026,
                    12,
                    24),
                new DateOnly(
                    2027,
                    1,
                    2),
                nightlyRate: 250m,
                currency: "USD",
                priority: 20);

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

        GuestCount guestCount =
            GuestCount.Create(2)
                .Value;

        // ACT
        Result<PriceBreakdown> result =
            BookingPricingEngine.CalculatePrice(
                pricing,
                maxBaseGuests: 2,
                guestCount,
                stayPeriod,
                [
                    firstSeason,
                    secondSeason
                ]);

        // ASSERT
        Assert.True(
            result.IsFailure);

        Assert.Equal(
            PricingSeasonErrors.AmbiguousPriority(
                night,
                20),
            result.Error);
    }

    [Fact]
    public void CalculatePrice_WhenSeasonUsesDifferentCurrency_ShouldReturnFailure()
    {
        // ARRANGE
        RentableUnitPricing pricing =
            CreatePricing();

        PricingSeason season =
            CreateSeason(
                new DateOnly(
                    2026,
                    12,
                    24),
                new DateOnly(
                    2026,
                    12,
                    25),
                nightlyRate: 200m,
                currency: "EUR",
                priority: 10);

        StayPeriod stayPeriod =
            StayPeriod.Create(
                    new DateOnly(
                        2026,
                        12,
                        24),
                    new DateOnly(
                        2026,
                        12,
                        25))
                .Value;

        GuestCount guestCount =
            GuestCount.Create(2)
                .Value;

        // ACT
        Result<PriceBreakdown> result =
            BookingPricingEngine.CalculatePrice(
                pricing,
                maxBaseGuests: 2,
                guestCount,
                stayPeriod,
                [season]);

        // ASSERT
        Assert.True(
            result.IsFailure);

        Assert.Equal(
            MoneyErrors.CurrencyMismatch(
                "USD",
                "EUR"),
            result.Error);
    }

    [Fact]
    public void CalculatePrice_WithInvalidMaxBaseGuests_ShouldThrow()
    {
        // ARRANGE
        RentableUnitPricing pricing =
            CreatePricing();

        GuestCount guestCount =
            GuestCount.Create(2)
                .Value;

        StayPeriod stayPeriod =
            CreateStayPeriod();

        // ACT
        void Action()
        {
            BookingPricingEngine.CalculatePrice(
                pricing,
                maxBaseGuests: 0,
                guestCount,
                stayPeriod,
                []);
        }

        // ASSERT
        Assert.Throws<
            ArgumentOutOfRangeException>(
                Action);
    }

    [Fact]
    public void CalculatePrice_WithNullPricing_ShouldThrow()
    {
        GuestCount guestCount =
            GuestCount.Create(2)
                .Value;

        StayPeriod stayPeriod =
            CreateStayPeriod();

        void Action()
        {
            BookingPricingEngine.CalculatePrice(
                null!,
                maxBaseGuests: 2,
                guestCount,
                stayPeriod,
                []);
        }

        Assert.Throws<
            ArgumentNullException>(
                Action);
    }

    [Fact]
    public void CalculatePrice_WithNullGuestCount_ShouldThrow()
    {
        RentableUnitPricing pricing =
            CreatePricing();

        StayPeriod stayPeriod =
            CreateStayPeriod();

        void Action()
        {
            BookingPricingEngine.CalculatePrice(
                pricing,
                maxBaseGuests: 2,
                null!,
                stayPeriod,
                []);
        }

        Assert.Throws<
            ArgumentNullException>(
                Action);
    }

    [Fact]
    public void CalculatePrice_WithNullStayPeriod_ShouldThrow()
    {
        RentableUnitPricing pricing =
            CreatePricing();

        GuestCount guestCount =
            GuestCount.Create(2)
                .Value;

        void Action()
        {
            BookingPricingEngine.CalculatePrice(
                pricing,
                maxBaseGuests: 2,
                guestCount,
                null!,
                []);
        }

        Assert.Throws<
            ArgumentNullException>(
                Action);
    }

    [Fact]
    public void CalculatePrice_WithNullSeasons_ShouldThrow()
    {
        RentableUnitPricing pricing =
            CreatePricing();

        GuestCount guestCount =
            GuestCount.Create(2)
                .Value;

        StayPeriod stayPeriod =
            CreateStayPeriod();

        void Action()
        {
            BookingPricingEngine.CalculatePrice(
                pricing,
                maxBaseGuests: 2,
                guestCount,
                stayPeriod,
                null!);
        }

        Assert.Throws<
            ArgumentNullException>(
                Action);
    }

    private static RentableUnitPricing CreatePricing()
    {
        return RentableUnitPricing.Create(
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
    }

    private static PricingSeason CreateSeason(
        DateOnly startDate,
        DateOnly endDate,
        decimal nightlyRate,
        string currency,
        int priority)
    {
        return PricingSeason.Create(
                startDate,
                endDate,
                Money.Create(
                        nightlyRate,
                        currency)
                    .Value,
                priority)
            .Value;
    }

    private static StayPeriod CreateStayPeriod()
    {
        return StayPeriod.Create(
                new DateOnly(
                    2026,
                    9,
                    10),
                new DateOnly(
                    2026,
                    9,
                    12))
            .Value;
    }
}
