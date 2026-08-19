using Bookify.Services.Booking.Application.Abstractions.Persistence;
using Bookify.Services.Booking.Application.Abstractions.Persistence.Repositories;
using Bookify.Services.Booking.Domain.Bookings.Pricing;
using Bookify.Services.Booking.Domain.Properties;
using Bookify.Services.Booking.Domain.Shared.ValueObjects;
using Bookify.Services.Booking.IntegrationTests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Bookify.Services.Booking.IntegrationTests.Properties;

[Collection(BookingApiTestFixture.Name)]
[Trait("Category", "Integration")]
public sealed class RentableUnitPricingSeasonPersistenceTests
{
    private readonly BookingApiFactory _factory;

    public RentableUnitPricingSeasonPersistenceTests(
        BookingApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task SaveAndReload_WithPricingSeasons_ShouldPreserveSeasons()
    {
        // ARRANGE
        CancellationToken cancellationToken =
            TestContext.Current.CancellationToken;

        Property property =
            Property.Create(
                "Season Persistence Property",
                "America/El_Salvador",
                new TimeOnly(
                    15,
                    0),
                new TimeOnly(
                    11,
                    0))
            .Value;

        RentableUnit rentableUnit =
            RentableUnit.Create(
                property.Id,
                "Room A",
                RentableUnitType.Room,
                maximumCapacity: 4,
                maxBaseGuests: 2)
            .Value;

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

        rentableUnit.AddPricingSeason(
            highSeason);

        rentableUnit.AddPricingSeason(
            christmas);

        using (
            IServiceScope seedScope =
                _factory.Services.CreateScope())
        {
            IPropertyRepository propertyRepository =
                seedScope
                    .ServiceProvider
                    .GetRequiredService<
                        IPropertyRepository>();

            IRentableUnitRepository rentableUnitRepository =
                seedScope
                    .ServiceProvider
                    .GetRequiredService<
                        IRentableUnitRepository>();

            IUnitOfWork unitOfWork =
                seedScope
                    .ServiceProvider
                    .GetRequiredService<
                        IUnitOfWork>();

            propertyRepository.Add(
                property);

            rentableUnitRepository.Add(
                rentableUnit);

            await unitOfWork.SaveChangesAsync(
                cancellationToken);
        }

        // ACT
        RentableUnit? persistedRentableUnit;

        using (
            IServiceScope assertionScope =
                _factory.Services.CreateScope())
        {
            IRentableUnitRepository rentableUnitRepository =
                assertionScope
                    .ServiceProvider
                    .GetRequiredService<
                        IRentableUnitRepository>();

            persistedRentableUnit =
                await rentableUnitRepository
                    .GetByIdAsync(
                        rentableUnit.Id,
                        cancellationToken);
        }

        // ASSERT
        Assert.NotNull(
            persistedRentableUnit);

        Assert.Equal(
            2,
            persistedRentableUnit
                .PricingSeasons
                .Count);

        PricingSeason persistedHighSeason =
            persistedRentableUnit
                .PricingSeasons
                .Single(
                    season =>
                        season.Priority == 10);

        Assert.Equal(
            new DateOnly(2026, 12, 20),
            persistedHighSeason.StartDate);

        Assert.Equal(
            new DateOnly(2027, 1, 5),
            persistedHighSeason.EndDate);

        Assert.Equal(
            180m,
            persistedHighSeason
                .NightlyRate
                .Amount);

        Assert.Equal(
            "USD",
            persistedHighSeason
                .NightlyRate
                .Currency);

        PricingSeason persistedChristmas =
            persistedRentableUnit
                .PricingSeasons
                .Single(
                    season =>
                        season.Priority == 20);

        Assert.Equal(
            new DateOnly(2026, 12, 24),
            persistedChristmas.StartDate);

        Assert.Equal(
            new DateOnly(2026, 12, 27),
            persistedChristmas.EndDate);

        Assert.Equal(
            250m,
            persistedChristmas
                .NightlyRate
                .Amount);

        Assert.Equal(
            "USD",
            persistedChristmas
                .NightlyRate
                .Currency);
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
