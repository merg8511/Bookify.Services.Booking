
using System.Data.Common;
using Bookify.Services.Booking.Application.Abstractions.Persistence;
using Bookify.Services.Booking.Infrastructure.Persistence;
using Dapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Bookify.Services.Booking.Integration.Tests.Infrastructure;

[Collection(BookingApiTestFixture.Name)]
[Trait("Category", "Integration")]
[Trait("Category", "Testcontainers")]
public sealed class PostgreSqlTestcontainersIsolationTests
{
    private readonly BookingApiFactory _factory;

    public PostgreSqlTestcontainersIsolationTests(
        BookingApiFactory factory)
    {
        _factory = factory ??
            throw new ArgumentNullException(nameof(factory));
    }

    [Fact]
    public async Task SharedFixture_ShouldUseRealExpectedPostgreSqlVersion()
    {
        // ARRANGE
        CancellationToken cancellationToken =
            TestContext.Current.CancellationToken;

        using IServiceScope scope =
            _factory.Services.CreateScope();

        BookingDbContext dbContext =
            scope.ServiceProvider
                .GetRequiredService<BookingDbContext>();

        IDbConnectionFactory connectionFactory =
            scope.ServiceProvider
                .GetRequiredService<IDbConnectionFactory>();

        // ASSERT — No usamos EF InMemory ni SQLite.
        Assert.Equal(
            "Npgsql.EntityFrameworkCore.PostgreSQL",
            dbContext.Database.ProviderName);

        // ACT — Consultar directamente el motor PostgreSQL.
        await using DbConnection connection =
            await connectionFactory.OpenConnectionAsync(
                cancellationToken);

        string? version =
            await connection.ExecuteScalarAsync<string>(
                new CommandDefinition(
                    "SHOW server_version;",
                    cancellationToken: cancellationToken));

        // ASSERT — Contrato de la imagen actual:
        // postgres:18.4-alpine.
        Assert.NotNull(version);

        Assert.StartsWith(
            "18.4",
            version,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task TwoIndependentContainers_ShouldNotShareDatabaseState()
    {
        // ARRANGE
        CancellationToken cancellationToken =
            TestContext.Current.CancellationToken;

        // Dos instancias separadas del helper existente.
        await using var firstDatabase =
            new PostgreSqlTestDatabase();

        await using var secondDatabase =
            new PostgreSqlTestDatabase();

        // Iniciamos ambos contenedores.
        await firstDatabase.StartAsync(
            cancellationToken);

        await secondDatabase.StartAsync(
            cancellationToken);

        // El nombre lógico de la base puede ser el mismo,
        // pero cada contenedor tiene su propio endpoint.
        Assert.NotEqual(
            firstDatabase.ConnectionString,
            secondDatabase.ConnectionString);

        await using ServiceProvider firstServices =
            IntegrationTestServiceProvider.Create(
                firstDatabase.ConnectionString);

        await using ServiceProvider secondServices =
            IntegrationTestServiceProvider.Create(
                secondDatabase.ConnectionString);

        IDbConnectionFactory firstConnectionFactory =
            firstServices.GetRequiredService<
                IDbConnectionFactory>();

        IDbConnectionFactory secondConnectionFactory =
            secondServices.GetRequiredService<
                IDbConnectionFactory>();

        await using DbConnection firstConnection =
            await firstConnectionFactory.OpenConnectionAsync(
                cancellationToken);

        await using DbConnection secondConnection =
            await secondConnectionFactory.OpenConnectionAsync(
                cancellationToken);

        // ACT 1 — Crear una tabla de prueba en PostgreSQL A.
        //
        // No ejecutamos migrations aquí porque únicamente
        // queremos comprobar aislamiento de contenedores.
        // El smoke test completo de migrations es 22.10.
        await firstConnection.ExecuteAsync(
            new CommandDefinition(
                """
                CREATE TABLE public.bookify_container_isolation_probe
                (
                    id integer PRIMARY KEY
                );
                """,
                cancellationToken: cancellationToken));

        await firstConnection.ExecuteAsync(
            new CommandDefinition(
                """
                INSERT INTO public.bookify_container_isolation_probe
                    (id)
                VALUES
                    (1);
                """,
                cancellationToken: cancellationToken));

        // ACT 2 — Consultar ambos contenedores.
        long firstCount =
            await firstConnection.ExecuteScalarAsync<long>(
                new CommandDefinition(
                    """
                    SELECT COUNT(*)
                    FROM public.bookify_container_isolation_probe;
                    """,
                    cancellationToken: cancellationToken));

        bool secondContainsProbe =
            await secondConnection.ExecuteScalarAsync<bool>(
                new CommandDefinition(
                    """
                    SELECT
                        to_regclass(
                            'public.bookify_container_isolation_probe'
                        ) IS NOT NULL;
                    """,
                    cancellationToken: cancellationToken));

        // ASSERT
        Assert.Equal(1L, firstCount);

        // La tabla escrita en A no existe en B.
        Assert.False(secondContainsProbe);

        // Al salir del método, await using libera:
        // conexiones, service providers y contenedores.
    }

    [Fact]
    public async Task TwoApiFactories_ShouldUseIndependentConfigurationsAndData()
    {
        // ARRANGE
        CancellationToken cancellationToken =
            TestContext.Current.CancellationToken;

        // La factoría A es la fixture ya inicializada
        // por xUnit.
        BookingApiFactory firstFactory = _factory;

        // La factoría B tendrá otro PostgreSQL.
        await using var secondFactory =
            new BookingApiFactory();

        await secondFactory.InitializeAsync();

        IConfiguration firstConfiguration =
            firstFactory.Services
                .GetRequiredService<IConfiguration>();

        IConfiguration secondConfiguration =
            secondFactory.Services
                .GetRequiredService<IConfiguration>();

        string? firstConfiguredConnection =
            firstConfiguration.GetConnectionString(
                "Database");

        string? secondConfiguredConnection =
            secondConfiguration.GetConnectionString(
                "Database");

        // ASSERT 1 — Cada host usa la cadena de su
        // propio contenedor, no una cadena global compartida.
        Assert.Equal(
            firstFactory.DatabaseConnectionString,
            firstConfiguredConnection);

        Assert.Equal(
            secondFactory.DatabaseConnectionString,
            secondConfiguredConnection);

        Assert.NotEqual(
            firstConfiguredConnection,
            secondConfiguredConnection);

        // ACT — Escribir datos en cada base independiente.
        SeedData firstProperty =
            await BookingDatabaseTestSeeder
                .SeedPropertyWithRoomAsync(
                    firstFactory,
                    propertyName:
                        $"Isolation A {Guid.NewGuid():N}",
                    cancellationToken: cancellationToken);

        SeedData secondProperty =
            await BookingDatabaseTestSeeder
                .SeedPropertyWithRoomAsync(
                    secondFactory,
                    propertyName:
                        $"Isolation B {Guid.NewGuid():N}",
                    cancellationToken: cancellationToken);

        Assert.NotEqual(
            firstProperty.PropertyId,
            secondProperty.PropertyId);

        // ASSERT 2 — La propiedad A existe únicamente en A.
        Assert.Equal(
            1L,
            await CountPropertyAsync(
                firstFactory.Services,
                firstProperty.PropertyId,
                cancellationToken));

        Assert.Equal(
            0L,
            await CountPropertyAsync(
                secondFactory.Services,
                firstProperty.PropertyId,
                cancellationToken));

        // ASSERT 3 — La propiedad B existe únicamente en B.
        Assert.Equal(
            1L,
            await CountPropertyAsync(
                secondFactory.Services,
                secondProperty.PropertyId,
                cancellationToken));

        Assert.Equal(
            0L,
            await CountPropertyAsync(
                firstFactory.Services,
                secondProperty.PropertyId,
                cancellationToken));
    }

    private static async Task<long> CountPropertyAsync(
        IServiceProvider services,
        Guid propertyId,
        CancellationToken cancellationToken)
    {
        IDbConnectionFactory connectionFactory =
            services.GetRequiredService<IDbConnectionFactory>();

        await using DbConnection connection =
            await connectionFactory.OpenConnectionAsync(
                cancellationToken);

        return await connection.ExecuteScalarAsync<long>(
            new CommandDefinition(
                """
                SELECT COUNT(*)
                FROM properties
                WHERE id = @PropertyId;
                """,
                new
                {
                    PropertyId = propertyId
                },
                cancellationToken: cancellationToken));
    }
}
