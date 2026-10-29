using Bookify.Services.Booking.Domain.Bookings.Services;
using Bookify.Services.Booking.Domain.Properties;
using Bookify.Services.Booking.Infrastructure.DemoSeeding;
using Bookify.Services.Booking.Infrastructure.Persistence;
using Bookify.Services.Booking.Integration.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Bookify.Services.Booking.Integration.Tests.DemoSeeding;

[Trait("Category", "Integration")]
[Trait("Category", "DemoSeeding")]
public sealed class CompletePropertyDemoSeedIntegrationTests
{
    [Fact]
    public async Task CompletePropertySeed_ShouldPersistUnitsPricingAndSeasons()
    {
        // ARRANGE
        CancellationToken cancellationToken =
            TestContext.Current.CancellationToken;

        await using var database =
            new PostgreSqlTestDatabase();

        await database.StartAsync(cancellationToken);

        IConfiguration configuration =
            new ConfigurationBuilder()
                .AddInMemoryCollection(
                    new Dictionary<string, string?>
                    {
                        ["Payments:Provider"] = "Fake",
                        ["DemoSeed:OwnerSubjectId"] =
                            "demo-owner-subject"
                    })
                .Build();

        await using ServiceProvider services =
            IntegrationTestServiceProvider.Create(
                database.ConnectionString,
                configuration);

        await IntegrationTestServiceProvider
            .ApplyMigrationsAsync(
                services,
                cancellationToken);

        // ACT
        // Ejecutar el lote real de 23.2 mediante
        // el runner implementado en 23.1.
        await using (AsyncServiceScope seedScope =
            services.CreateAsyncScope())
        {
            DemoSeedRunner runner =
                seedScope.ServiceProvider
                    .GetRequiredService<DemoSeedRunner>();

            int appliedBatches =
                await runner.RunAsync(cancellationToken);

            Assert.Equal(1, appliedBatches);
        }

        // ASSERT
        // Consultar de nuevo PostgreSQL con otro DbContext.
        await using AsyncServiceScope assertionScope =
            services.CreateAsyncScope();

        BookingDbContext dbContext =
            assertionScope.ServiceProvider
                .GetRequiredService<BookingDbContext>();

        Property property = Assert.Single(
            await dbContext.Properties
                .AsNoTracking()
                .ToArrayAsync(cancellationToken));

        Assert.Equal(
            "Bookify Demo - Casa Costa Azul",
            property.Name);

        Assert.Equal(
            "America/El_Salvador",
            property.TimeZoneId);

        Assert.Equal(
            "demo-owner-subject",
            property.OwnerSubjectId);

        Assert.Equal(
            new TimeOnly(15, 0),
            property.CheckInTime);

        Assert.Equal(
            new TimeOnly(11, 0),
            property.CheckOutTime);

        Assert.True(property.IsActive);

        // Cargar RentableUnits con pricing y seasons.
        RentableUnit[] units =
            await dbContext.RentableUnits
                .AsNoTracking()
                .Include(unit => unit.Pricing)
                .Include(unit => unit.PricingSeasons)
                .ToArrayAsync(cancellationToken);

        // Una propiedad, tres habitaciones y
        // una unidad EntireProperty.
        Assert.Equal(4, units.Length);

        Assert.All(
            units,
            unit => Assert.Equal(
                property.Id,
                unit.PropertyId));

        RentableUnit[] rooms =
            units
                .Where(unit =>
                    unit.Type == RentableUnitType.Room)
                .ToArray();

        Assert.Equal(3, rooms.Length);

        RentableUnit entireProperty = Assert.Single(units, unit =>
                unit.Type ==
                    RentableUnitType.EntireProperty);

        Assert.Equal(
            9,
            entireProperty.MaximumCapacity);

        Assert.Equal(
            6,
            entireProperty.MaxBaseGuests);

        // Las capacidades físicas de las habitaciones
        // coinciden con la capacidad de la casa completa.
        Assert.Equal(
            9,
            rooms.Sum(room => room.MaximumCapacity));

        // Todas las unidades tienen pricing completo
        // y exactamente dos temporadas activas.
        Assert.All(
            units,
            unit =>
            {
                Assert.True(unit.IsActive);
                Assert.NotNull(unit.Pricing);

                Assert.Equal(
                    "USD",
                    unit.Pricing.RegularNightlyRate.Currency);

                Assert.Equal(
                    "USD",
                    unit.Pricing.WeekendNightlyRate.Currency);

                Assert.Equal(
                    "USD",
                    unit.Pricing.ExtraGuestNightlyRate.Currency);

                Assert.Equal(
                    2,
                    unit.PricingSeasons.Count);

                Assert.All(
                    unit.PricingSeasons,
                    season => Assert.True(
                        season.IsActive));

                var highSeason =
                    unit.PricingSeasons.Single(
                        season => season.Priority == 10);

                var specialSeason =
                    unit.PricingSeasons.Single(
                        season => season.Priority == 20);

                Assert.True(
                    specialSeason.StartDate >
                    highSeason.StartDate);

                Assert.True(
                    specialSeason.EndDate <
                    highSeason.EndDate);

                Assert.Equal(
                    "USD",
                    highSeason.NightlyRate.Currency);

                Assert.Equal(
                    "USD",
                    specialSeason.NightlyRate.Currency);
            });

        // Comprobar tarifas concretas.
        RentableUnit coralRoom =
            units.Single(unit =>
                unit.Name == "Habitación Coral");

        Assert.Equal(
            85m,
            coralRoom.Pricing!.RegularNightlyRate.Amount);

        Assert.Equal(
            105m,
            coralRoom.Pricing.WeekendNightlyRate.Amount);

        Assert.Equal(
            20m,
            coralRoom.Pricing.ExtraGuestNightlyRate.Amount);

        Assert.Equal(
            350m,
            entireProperty.Pricing!.RegularNightlyRate.Amount);

        Assert.Equal(
            425m,
            entireProperty.Pricing.WeekendNightlyRate.Amount);

        // Verificar que el motor de pricing existente
        // elige la temporada de mayor prioridad.
        var specialPricingSeason =
            coralRoom.PricingSeasons.Single(
                season => season.Priority == 20);

        var resolvedRate =
            SeasonPricingPolicy.ResolveNightlyRate(
                specialPricingSeason.StartDate,
                coralRoom.Pricing.RegularNightlyRate,
                coralRoom.PricingSeasons);

        Assert.True(resolvedRate.IsSuccess);

        Assert.Equal(
            127.50m,
            resolvedRate.Value.Amount);

        Assert.Equal(
            "USD",
            resolvedRate.Value.Currency);
    }
}
