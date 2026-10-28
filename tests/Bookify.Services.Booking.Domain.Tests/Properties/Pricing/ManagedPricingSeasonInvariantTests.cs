
using Bookify.Services.Booking.Domain.Bookings.Errors;
using Bookify.Services.Booking.Domain.Bookings.Pricing;
using Bookify.Services.Booking.Domain.Properties;
using Bookify.Services.Booking.Domain.Properties.Pricing;
using Bookify.Services.Booking.Domain.Shared;
using Bookify.Services.Booking.Domain.Shared.ValueObjects;

namespace Bookify.Services.Booking.Domain.Tests.Properties.Pricing;

public sealed class ManagedPricingSeasonInvariantTests
{
    [Fact]
    public void AddManagedSeason_WithoutPricing_ShouldRejectWithoutMutation()
    {
        // ARRANGE
        RentableUnit unit = CreateUnit();

        PricingSeason season = CreateSeason(
            20, 28, 180m, priority: 10);

        // ACT
        Result result = unit.AddManagedPricingSeason(season);

        // ASSERT
        Assert.True(result.IsFailure);
        Assert.Equal(
            PricingSeasonErrors.PricingNotConfigured,
            result.Error);

        Assert.Empty(unit.PricingSeasons);
    }

    [Fact]
    public void AddManagedSeason_WithDifferentCurrency_ShouldRejectInactiveSeasonToo()
    {
        // ARRANGE
        RentableUnit unit = CreateConfiguredUnit();

        PricingSeason season = CreateSeason(
            20, 28, 180m, priority: 10, currency: "EUR");

        season.Deactivate();

        // ACT
        Result result = unit.AddManagedPricingSeason(season);

        // ASSERT
        Assert.True(result.IsFailure);
        Assert.Equal(
            PricingSeasonErrors.CurrencyMismatch,
            result.Error);

        Assert.Empty(unit.PricingSeasons);
    }

    [Fact]
    public void AddManagedSeason_InactiveCandidate_ShouldAllowOverlap()
    {
        // ARRANGE
        RentableUnit unit = CreateConfiguredUnit();

        PricingSeason active = CreateSeason(
            20, 28, 180m, priority: 10);

        PricingSeason inactive = CreateSeason(
            24, 27, 250m, priority: 10);

        inactive.Deactivate();

        Assert.True(
            unit.AddManagedPricingSeason(active).IsSuccess);

        // ACT
        Result result =
            unit.AddManagedPricingSeason(inactive);

        // ASSERT
        Assert.True(result.IsSuccess);
        Assert.Equal(2, unit.PricingSeasons.Count);
        Assert.True(active.IsActive);
        Assert.False(inactive.IsActive);
    }

    [Fact]
    public void AddManagedSeason_WhenExistingIsInactive_ShouldAllowOverlap()
    {
        // ARRANGE
        RentableUnit unit = CreateConfiguredUnit();

        PricingSeason inactive = CreateSeason(
            20, 28, 180m, priority: 10);

        inactive.Deactivate();

        PricingSeason active = CreateSeason(
            24, 27, 250m, priority: 10);

        Assert.True(
            unit.AddManagedPricingSeason(inactive).IsSuccess);

        // ACT
        Result result =
            unit.AddManagedPricingSeason(active);

        // ASSERT
        Assert.True(result.IsSuccess);
        Assert.Equal(2, unit.PricingSeasons.Count);
        Assert.False(inactive.IsActive);
        Assert.True(active.IsActive);
    }

    [Fact]
    public void UpdateManagedSeason_WhenNotFound_ShouldNotMutateCollection()
    {
        // ARRANGE
        RentableUnit unit = CreateConfiguredUnit();

        // ACT
        Result result = unit.UpdateManagedPricingSeason(
            404,
            Day(20),
            Day(28),
            Money.Create(200m, "USD").Value,
            priority: 10);

        // ASSERT
        Assert.True(result.IsFailure);
        Assert.Equal(
            PricingSeasonErrors.NotFound(404),
            result.Error);

        Assert.Empty(unit.PricingSeasons);
    }

    [Fact]
    public void SetManagedSeasonStatus_WhenNotFound_ShouldReturnFailure()
    {
        // ARRANGE
        RentableUnit unit = CreateConfiguredUnit();

        // ACT
        Result result = unit.SetManagedPricingSeasonStatus(
            404,
            isActive: true);

        // ASSERT
        Assert.True(result.IsFailure);
        Assert.Equal(
            PricingSeasonErrors.NotFound(404),
            result.Error);

        Assert.Empty(unit.PricingSeasons);
    }

    [Fact]
    public void UpdateManagedSeason_WithCurrencyMismatch_ShouldPreserveSeason()
    {
        // ARRANGE
        RentableUnit unit = CreateConfiguredUnit();

        PricingSeason season = CreateSeason(
            20, 28, 180m, priority: 10);

        Assert.True(
            unit.AddManagedPricingSeason(season).IsSuccess);

        // ACT
        Result result = unit.UpdateManagedPricingSeason(
            season.Id,
            Day(21),
            Day(29),
            Money.Create(250m, "EUR").Value,
            priority: 20);

        // ASSERT
        Assert.True(result.IsFailure);
        Assert.Equal(
            PricingSeasonErrors.CurrencyMismatch,
            result.Error);

        Assert.Same(season, Assert.Single(unit.PricingSeasons));
        Assert.Equal(Day(20), season.StartDate);
        Assert.Equal(Day(28), season.EndDate);
        Assert.Equal(180m, season.NightlyRate.Amount);
        Assert.Equal("USD", season.NightlyRate.Currency);
        Assert.Equal(10, season.Priority);
        Assert.True(season.IsActive);
    }

    [Fact]
    public void UpdateManagedSeason_WithInvalidRange_ShouldPreserveSeason()
    {
        // ARRANGE
        RentableUnit unit = CreateConfiguredUnit();

        PricingSeason season = CreateSeason(
            20, 28, 180m, priority: 10);

        Assert.True(
            unit.AddManagedPricingSeason(season).IsSuccess);

        // ACT
        Result result = unit.UpdateManagedPricingSeason(
            season.Id,
            Day(25),
            Day(25),
            Money.Create(250m, "USD").Value,
            priority: 20);

        // ASSERT
        Assert.True(result.IsFailure);
        Assert.Equal(
            PricingSeasonErrors.InvalidDateRange,
            result.Error);

        Assert.Same(season, Assert.Single(unit.PricingSeasons));
        Assert.Equal(Day(20), season.StartDate);
        Assert.Equal(Day(28), season.EndDate);
        Assert.Equal(180m, season.NightlyRate.Amount);
        Assert.Equal(10, season.Priority);
        Assert.True(season.IsActive);
    }

    [Fact]
    public void UpdateManagedSeason_WhenInactive_ShouldPreserveActivationState()
    {
        // ARRANGE
        RentableUnit unit = CreateConfiguredUnit();

        PricingSeason season = CreateSeason(
            20, 28, 180m, priority: 10);

        season.Deactivate();

        Assert.True(
            unit.AddManagedPricingSeason(season).IsSuccess);

        // ACT
        Result result = unit.UpdateManagedPricingSeason(
            season.Id,
            Day(21),
            Day(29),
            Money.Create(220m, "USD").Value,
            priority: 20);

        // ASSERT
        Assert.True(result.IsSuccess);

        Assert.Same(season, Assert.Single(unit.PricingSeasons));
        Assert.Equal(Day(21), season.StartDate);
        Assert.Equal(Day(29), season.EndDate);
        Assert.Equal(220m, season.NightlyRate.Amount);
        Assert.Equal(20, season.Priority);
        Assert.False(season.IsActive);
    }

    [Fact]
    public void PricingSeason_UpdateWithInvalidPriority_ShouldPreserveState()
    {
        // ARRANGE
        PricingSeason season = CreateSeason(
            20, 28, 180m, priority: 10);

        // ACT
        Result result = season.Update(
            Day(21),
            Day(29),
            Money.Create(250m, "USD").Value,
            priority: -1);

        // ASSERT
        Assert.True(result.IsFailure);
        Assert.Equal(
            PricingSeasonErrors.InvalidPriority,
            result.Error);

        Assert.Equal(Day(20), season.StartDate);
        Assert.Equal(Day(28), season.EndDate);
        Assert.Equal(180m, season.NightlyRate.Amount);
        Assert.Equal(10, season.Priority);
        Assert.True(season.IsActive);
    }

    private static DateOnly Day(int day) =>
        new(2026, 12, day);

    private static PricingSeason CreateSeason(
        int startDay,
        int endDay,
        decimal amount,
        int priority,
        string currency = "USD")
    {
        return PricingSeason.Create(
            Day(startDay),
            Day(endDay),
            Money.Create(amount, currency).Value,
            priority).Value;
    }

    private static RentableUnit CreateUnit()
    {
        return RentableUnit.Create(
            Guid.NewGuid(),
            "Suite A",
            RentableUnitType.Room,
            maximumCapacity: 4,
            maxBaseGuests: 2).Value;
    }

    private static RentableUnit CreateConfiguredUnit()
    {
        RentableUnit unit = CreateUnit();

        RentableUnitPricing pricing =
            RentableUnitPricing.Create(
                Money.Create(100m, "USD").Value,
                Money.Create(140m, "USD").Value,
                Money.Create(25m, "USD").Value).Value;

        unit.ConfigurePricing(pricing);

        return unit;
    }
}
