using Bookify.Services.Booking.Domain.Bookings.Pricing;
using Bookify.Services.Booking.Domain.Properties;
using Bookify.Services.Booking.Domain.Shared.ValueObjects;

namespace Bookify.Services.Booking.Domain.Tests.Properties.Pricing;

public sealed class RentableUnitPricingSeasonTests
{
    [Fact]
    public void Create_ShouldStartWithoutPricingSeasons()
    {
        // ACT
        RentableUnit rentableUnit =
            CreateRentableUnit();

        // ASSERT
        Assert.Empty(
            rentableUnit.PricingSeasons);
    }

    [Fact]
    public void AddPricingSeason_WithValidSeason_ShouldAddSeason()
    {
        // ARRANGE
        RentableUnit rentableUnit =
            CreateRentableUnit();

        PricingSeason season =
            CreateSeason(
                new DateOnly(2026, 12, 20),
                new DateOnly(2027, 1, 5),
                nightlyRate: 180m,
                priority: 10);

        // ACT
        rentableUnit.AddPricingSeason(
            season);

        // ASSERT
        PricingSeason storedSeason =
            Assert.Single(
                rentableUnit.PricingSeasons);

        Assert.Equal(
            season,
            storedSeason);
    }

    [Fact]
    public void AddPricingSeason_WithOverlappingSeasons_ShouldPreserveBoth()
    {
        // ARRANGE
        RentableUnit rentableUnit =
            CreateRentableUnit();

        PricingSeason highSeason =
            CreateSeason(
                new DateOnly(2026, 12, 20),
                new DateOnly(2027, 1, 5),
                nightlyRate: 180m,
                priority: 10);

        PricingSeason christmas =
            CreateSeason(
                new DateOnly(2026, 12, 24),
                new DateOnly(2026, 12, 27),
                nightlyRate: 250m,
                priority: 20);

        // ACT
        rentableUnit.AddPricingSeason(
            highSeason);

        rentableUnit.AddPricingSeason(
            christmas);

        // ASSERT
        Assert.Equal(
            2,
            rentableUnit.PricingSeasons.Count);

        Assert.Contains(
            highSeason,
            rentableUnit.PricingSeasons);

        Assert.Contains(
            christmas,
            rentableUnit.PricingSeasons);
    }

    [Fact]
    public void AddPricingSeason_WithNullSeason_ShouldThrow()
    {
        // ARRANGE
        RentableUnit rentableUnit =
            CreateRentableUnit();

        // ACT
        void Action()
        {
            rentableUnit.AddPricingSeason(
                null!);
        }

        // ASSERT
        Assert.Throws<ArgumentNullException>(
            Action);
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
