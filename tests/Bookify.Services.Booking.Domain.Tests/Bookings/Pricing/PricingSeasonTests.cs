using Bookify.Services.Booking.Domain.Bookings.Errors;
using Bookify.Services.Booking.Domain.Bookings.Pricing;
using Bookify.Services.Booking.Domain.Shared.ValueObjects;

namespace Bookify.Services.Booking.Domain.Tests.Bookings.Pricing;

public sealed class PricingSeasonTests
{
    [Fact]
    public void Create_WithValidData_ShouldReturnSeason()
    {
        // ARRANGE
        Money nightlyRate =
            Money.Create(
                220m,
                "USD")
            .Value;

        // ACT
        var result =
            PricingSeason.Create(
                new DateOnly(2026, 12, 24),
                new DateOnly(2026, 12, 28),
                nightlyRate,
                priority: 10);

        // ASSERT
        Assert.True(result.IsSuccess);

        Assert.Equal(
            new DateOnly(2026, 12, 24),
            result.Value.StartDate);

        Assert.Equal(
            new DateOnly(2026, 12, 28),
            result.Value.EndDate);

        Assert.Equal(
            nightlyRate,
            result.Value.NightlyRate);

        Assert.Equal(
            10,
            result.Value.Priority);
    }

    [Fact]
    public void Create_WithInvalidDateRange_ShouldReturnFailure()
    {
        // ARRANGE
        Money nightlyRate =
            Money.Create(
                220m,
                "USD")
            .Value;

        // ACT
        var result =
            PricingSeason.Create(
                new DateOnly(2026, 12, 28),
                new DateOnly(2026, 12, 24),
                nightlyRate,
                priority: 10);

        // ASSERT
        Assert.True(result.IsFailure);

        Assert.Equal(
            PricingSeasonErrors.InvalidDateRange,
            result.Error);
    }

    [Fact]
    public void Create_WithNegativePriority_ShouldReturnFailure()
    {
        // ARRANGE
        Money nightlyRate =
            Money.Create(
                220m,
                "USD")
            .Value;

        // ACT
        var result =
            PricingSeason.Create(
                new DateOnly(2026, 12, 24),
                new DateOnly(2026, 12, 28),
                nightlyRate,
                priority: -1);

        // ASSERT
        Assert.True(result.IsFailure);

        Assert.Equal(
            PricingSeasonErrors.InvalidPriority,
            result.Error);
    }

    [Fact]
    public void ContainsNight_ShouldUseInclusiveStartAndExclusiveEnd()
    {
        // ARRANGE
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

        // ACT
        bool containsStart =
            season.ContainsNight(
                new DateOnly(2026, 12, 24));

        bool containsLastNight =
            season.ContainsNight(
                new DateOnly(2026, 12, 27));

        bool containsEnd =
            season.ContainsNight(
                new DateOnly(2026, 12, 28));

        // ASSERT
        Assert.True(containsStart);
        Assert.True(containsLastNight);
        Assert.False(containsEnd);
    }
}
