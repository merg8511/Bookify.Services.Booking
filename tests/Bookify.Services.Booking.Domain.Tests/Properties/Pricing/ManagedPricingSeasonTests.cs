using Bookify.Services.Booking.Domain.Bookings.Pricing;
using Bookify.Services.Booking.Domain.Bookings.Services;
using Bookify.Services.Booking.Domain.Properties;
using Bookify.Services.Booking.Domain.Properties.Pricing;
using Bookify.Services.Booking.Domain.Shared;
using Bookify.Services.Booking.Domain.Shared.ValueObjects;

namespace Bookify.Services.Booking.Domain.Tests.Properties.Pricing;

public sealed class ManagedPricingSeasonTests
{
    private static RentableUnit CreateConfiguredUnit()
    {
        RentableUnit unit = RentableUnit.Create(
            Guid.NewGuid(),
            "Suite A",
            RentableUnitType.Room,
            maximumCapacity: 4,
            maxBaseGuests: 2).Value;

        RentableUnitPricing pricing = RentableUnitPricing.Create(
                    Money.Create(100m, "USD").Value,
                    Money.Create(140m, "USD").Value,
                    Money.Create(25m, "USD").Value).Value;

        unit.ConfigurePricing(pricing);

        return unit;
    }

    private static PricingSeason CreateSeason(
            int startDay,
            int endDay,
            decimal amount,
            int priority)
    {
        return PricingSeason.Create(
            new DateOnly(2026, 12, startDay),
            new DateOnly(2026, 12, endDay),
            Money.Create(amount, "USD").Value,
            priority).Value;
    }

    [Fact]
    public void AddManagedPricingSeason_WithDifferentPriorities_ShouldAllowOverlap()
    {
        // ARRANGE
        RentableUnit unit = CreateConfiguredUnit();

        PricingSeason first = CreateSeason(
                    startDay: 20,
                    endDay: 30,
                    amount: 180m,
                    priority: 10);

        PricingSeason second = CreateSeason(
                    startDay: 24,
                    endDay: 27,
                    amount: 250m,
                    priority: 20);

        // ACT
        Result firstResult = unit.AddManagedPricingSeason(first);
        Result secondResult = unit.AddManagedPricingSeason(second);

        // ASSERT
        Assert.True(firstResult.IsSuccess);
        Assert.True(secondResult.IsSuccess);
        Assert.Equal(2, unit.PricingSeasons.Count);
    }

    [Fact]
    public void AddManagedPricingSeason_WithSamePriorityOverlap_ShouldReject()
    {
        // ARRANGE
        RentableUnit unit = CreateConfiguredUnit();

        PricingSeason first = CreateSeason(
                    20, 30, 180m, 10);

        PricingSeason second = CreateSeason(
                    24, 27, 250m, 10);

        unit.AddManagedPricingSeason(first);

        // ACT
        Result result = unit.AddManagedPricingSeason(second);

        // ASSERT
        Assert.True(result.IsFailure);
        Assert.Equal(
            "PricingSeason.AmbiguousPriority",
            result.Error.Code);

        Assert.Single(unit.PricingSeasons);
    }

    [Fact]
    public void AddManagedPricingSeason_WithAdjacentDates_ShouldAllowSamePriority()
    {
        // ARRANGE
        RentableUnit unit = CreateConfiguredUnit();

        PricingSeason first = CreateSeason(
                    20, 24, 180m, 10);

        PricingSeason second = CreateSeason(
                    24, 27, 200m, 10);

        // ACT
        Result firstResult = unit.AddManagedPricingSeason(first);
        Result secondResult = unit.AddManagedPricingSeason(second);

        // ASSERT
        Assert.True(firstResult.IsSuccess);
        Assert.True(secondResult.IsSuccess);
        Assert.Equal(2, unit.PricingSeasons.Count);
    }

    [Fact]
    public void InactiveSeason_ShouldNotOverrideBaseRate()
    {
        // ARRANGE
        PricingSeason season = CreateSeason(
            20, 30, 250m, 10);

        season.Deactivate();

        Money fallback = Money.Create(100m, "USD").Value;

        // ACT
        Result<Money> result =
            SeasonPricingPolicy.ResolveNightlyRate(
                new DateOnly(2026, 12, 25),
                fallback,
                [season]);

        // ASSERT
        Assert.True(result.IsSuccess);
        Assert.Equal(100m, result.Value.Amount);
        Assert.Equal("USD", result.Value.Currency);
    }

    [Fact]
    public void SetManagedPricingSeasonStatus_ShouldDeactivateAndReactivate()
    {
        // ARRANGE
        RentableUnit unit = CreateConfiguredUnit();

        PricingSeason first = CreateSeason(
                    20, 30, 180m, 10);

        unit.AddManagedPricingSeason(first);

        // A persisted season has a generated ID.
        // This test exercises the lifecycle method on PricingSeason
        // without pretending the in-memory Id has been persisted.
        first.Deactivate();

        // ACT
        Result<Money> inactiveRate =
            SeasonPricingPolicy.ResolveNightlyRate(
                new DateOnly(2026, 12, 25),
                Money.Create(100m, "USD").Value,
                unit.PricingSeasons);

        first.Activate();

        Result<Money> activeRate =
                    SeasonPricingPolicy.ResolveNightlyRate(
                        new DateOnly(2026, 12, 25),
                        Money.Create(100m, "USD").Value,
                        unit.PricingSeasons);

        // ASSERT
        Assert.Equal(100m, inactiveRate.Value.Amount);
        Assert.Equal(180m, activeRate.Value.Amount);
    }
}
