using Bookify.Services.Booking.Domain.Bookings.Errors;
using Bookify.Services.Booking.Domain.Bookings.Pricing;
using Bookify.Services.Booking.Domain.Bookings.Services;
using Bookify.Services.Booking.Domain.Shared.ValueObjects;

namespace Bookify.Services.Booking.Domain.Tests.Bookings.Pricing;

public sealed class SeasonPricingPolicyTests
{
    [Fact]
    public void ResolveNightlyRate_WhenNoSeasonApplies_ShouldReturnFallbackRate()
    {
        // ARRANGE
        Money fallbackRate =
            Money.Create(
                100m,
                "USD")
            .Value;

        PricingSeason season =
            CreateSeason(
                new DateOnly(2026, 12, 24),
                new DateOnly(2026, 12, 28),
                220m,
                priority: 10);

        // ACT
        var result =
            SeasonPricingPolicy.ResolveNightlyRate(
                new DateOnly(2026, 12, 20),
                fallbackRate,
                [season]);

        // ASSERT
        Assert.True(result.IsSuccess);

        Assert.Equal(
            100m,
            result.Value.Amount);
    }

    [Fact]
    public void ResolveNightlyRate_WhenSeasonApplies_ShouldReturnSeasonRate()
    {
        // ARRANGE
        Money fallbackRate =
            Money.Create(
                100m,
                "USD")
            .Value;

        PricingSeason season =
            CreateSeason(
                new DateOnly(2026, 12, 24),
                new DateOnly(2026, 12, 28),
                220m,
                priority: 10);

        // ACT
        var result =
            SeasonPricingPolicy.ResolveNightlyRate(
                new DateOnly(2026, 12, 25),
                fallbackRate,
                [season]);

        // ASSERT
        Assert.True(result.IsSuccess);

        Assert.Equal(
            220m,
            result.Value.Amount);
    }

    [Fact]
    public void ResolveNightlyRate_WithOverlappingSeasons_ShouldUseHighestPriority()
    {
        // ARRANGE
        Money fallbackRate =
            Money.Create(
                100m,
                "USD")
            .Value;

        PricingSeason highSeason =
            CreateSeason(
                new DateOnly(2026, 12, 20),
                new DateOnly(2027, 1, 5),
                180m,
                priority: 10);

        PricingSeason christmas =
            CreateSeason(
                new DateOnly(2026, 12, 24),
                new DateOnly(2026, 12, 27),
                250m,
                priority: 20);

        // ACT
        var result =
            SeasonPricingPolicy.ResolveNightlyRate(
                new DateOnly(2026, 12, 25),
                fallbackRate,
                [
                    highSeason,
                    christmas
                ]);

        // ASSERT
        Assert.True(result.IsSuccess);

        Assert.Equal(
            250m,
            result.Value.Amount);
    }

    [Fact]
    public void ResolveNightlyRate_WithSameHighestPriority_ShouldReturnFailure()
    {
        // ARRANGE
        Money fallbackRate =
            Money.Create(
                100m,
                "USD")
            .Value;

        PricingSeason firstSeason =
            CreateSeason(
                new DateOnly(2026, 12, 20),
                new DateOnly(2026, 12, 30),
                200m,
                priority: 20);

        PricingSeason secondSeason =
            CreateSeason(
                new DateOnly(2026, 12, 24),
                new DateOnly(2027, 1, 2),
                250m,
                priority: 20);

        var night =
            new DateOnly(
                2026,
                12,
                25);

        // ACT
        var result =
            SeasonPricingPolicy.ResolveNightlyRate(
                night,
                fallbackRate,
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
