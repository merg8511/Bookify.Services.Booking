
using Bookify.Services.Booking.Api.DemoSeeding;
using Bookify.Services.Booking.Application;
using Bookify.Services.Booking.Infrastructure;
using Bookify.Services.Booking.Infrastructure.Persistence;
using Bookify.Services.Booking.Integration.Tests.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Bookify.Services.Booking.Integration.Tests.DemoSeeding;

[Trait("Category", "Integration")]
[Trait("Category", "DemoSeeding")]
public sealed class DemoSeedEnvironmentProtectionTests
{
    // ==========================================
    // TEST 1 — Ambientes prohibidos
    // ==========================================
    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    [InlineData("Testing")]
    public async Task EnabledSeed_ShouldRejectForbiddenEnvironment(
        string environmentName)
    {
        // ARRANGE
        // No registramos PostgreSQL ni DemoSeedRunner.
        // Si el guard funciona, falla antes de resolverlos.
        using WebApplication app = CreateWebApplication(
            environmentName,
            enabled: true);

        // ACT
        InvalidOperationException exception =
            await Assert.ThrowsAsync<
                InvalidOperationException>(
                () => app.RunDemoSeedAsync());

        // ASSERT
        Assert.Contains(
            "Development or Demo",
            exception.Message, StringComparison.Ordinal);
    }

    // ==========================================
    // TEST 2 — Feature flag deshabilitado
    // ==========================================
    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    [InlineData("Testing")]
    [InlineData("Development")]
    [InlineData("Demo")]
    public async Task DisabledSeed_ShouldReturnWithoutResolvingServices(
        string environmentName)
    {
        // ARRANGE
        // No configuramos conexión ni servicios de seed.
        // La extensión debe finalizar inmediatamente.
        using WebApplication app = CreateWebApplication(
            environmentName,
            enabled: false);

        // ACT
        await app.RunDemoSeedAsync();

        // ASSERT
        // Si intentara resolver DemoSeedRunner o DbContext,
        // esta prueba fallaría por falta de servicios.
        Assert.False(
            app.Configuration.GetValue<bool>(
                "DemoSeed:Enabled"));
    }

    // ==========================================
    // TEST 3 — Ambientes permitidos
    // ==========================================
    [Theory]
    [InlineData("Development")]
    [InlineData("Demo")]
    public async Task EnabledSeed_ShouldRunInAllowedEnvironment(
        string environmentName)
    {
        // ARRANGE
        CancellationToken cancellationToken =
            TestContext.Current.CancellationToken;

        await using var database =
            new PostgreSqlTestDatabase();

        await database.StartAsync(cancellationToken);

        var builder = WebApplication.CreateBuilder(
            new WebApplicationOptions
            {
                EnvironmentName = environmentName
            });

        ConfigureDemoSeed(builder.Configuration, enabled: true);

        // Registro real de Application + Infrastructure.
        builder.Services
            .AddApplication()
            .AddInfrastructure(
                database.ConnectionString,
                builder.Configuration);

        using WebApplication app = builder.Build();

        // Preparar migraciones antes de ejecutar el seed.
        await IntegrationTestServiceProvider
            .ApplyMigrationsAsync(
                app.Services,
                cancellationToken);

        // ACT
        await app.RunDemoSeedAsync();

        // ASSERT — Todos los lotes aplicados.
        await using (AsyncServiceScope scope =
            app.Services.CreateAsyncScope())
        {
            BookingDbContext dbContext =
                scope.ServiceProvider
                    .GetRequiredService<BookingDbContext>();

            Assert.Equal(
                1,
                await dbContext.Properties.CountAsync(
                    cancellationToken));

            Assert.Equal(
                4,
                await dbContext.RentableUnits.CountAsync(
                    cancellationToken));

            Assert.Equal(
                10,
                await dbContext.Bookings.CountAsync(
                    cancellationToken));

            Assert.Equal(
                2,
                await dbContext.Payments.CountAsync(
                    cancellationToken));
        }

        // ACT — Segunda ejecución de la extensión.
        // Además de ejecutar el runner, esta extensión
        // vuelve a auditar los Payments de 23.5.
        await app.RunDemoSeedAsync();

        // ASSERT — Continúa sin duplicados.
        await using (AsyncServiceScope scope =
            app.Services.CreateAsyncScope())
        {
            BookingDbContext dbContext =
                scope.ServiceProvider
                    .GetRequiredService<BookingDbContext>();

            int ledgerCount =
                await dbContext.Database
                    .SqlQueryRaw<int>(
                        """
                        SELECT COUNT(*)::int AS "Value"
                        FROM demo_seed_batches
                        """)
                    .SingleAsync(cancellationToken);

            Assert.Equal(4, ledgerCount);

            Assert.Equal(
                10,
                await dbContext.Bookings.CountAsync(
                    cancellationToken));
        }
    }

    // ==========================================
    // HELPERS
    // ==========================================
    private static WebApplication CreateWebApplication(
        string environmentName,
        bool enabled)
    {
        var builder = WebApplication.CreateBuilder(
            new WebApplicationOptions
            {
                EnvironmentName = environmentName
            });

        builder.Configuration["DemoSeed:Enabled"] =
            enabled.ToString();

        return builder.Build();
    }

    private static void ConfigureDemoSeed(
        ConfigurationManager configuration,
        bool enabled)
    {
        configuration["DemoSeed:Enabled"] =
            enabled.ToString();

        configuration["Payments:Provider"] =
            "Fake";

        configuration["DemoSeed:OwnerSubjectId"] =
            "demo-startup-owner";

        configuration["DemoSeed:CustomerSubjectId"] =
            "demo-startup-customer";
    }
}
