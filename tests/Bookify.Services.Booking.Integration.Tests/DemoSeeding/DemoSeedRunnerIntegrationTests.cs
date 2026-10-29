using Bookify.Services.Booking.Application.Abstractions.Persistence;
using Bookify.Services.Booking.Application.Abstractions.Time;
using Bookify.Services.Booking.Domain.Properties;
using Bookify.Services.Booking.Infrastructure.DemoSeeding;
using Bookify.Services.Booking.Infrastructure.Persistence;
using Bookify.Services.Booking.Integration.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;


namespace Bookify.Services.Booking.Integration.Tests.DemoSeeding;

[Trait("Category", "Integration")]
[Trait("Category", "DemoSeeding")]
public sealed class DemoSeedRunnerIntegrationTests
{
    [Fact]
    public async Task RunningSameBatchTwice_ShouldNotDuplicateOrOverwriteData()
    {
        // ARRANGE
        CancellationToken cancellationToken =
        TestContext.Current.CancellationToken;

        await using var database = new PostgreSqlTestDatabase();
        await database.StartAsync(cancellationToken);

        await using ServiceProvider services =
        IntegrationTestServiceProvider.Create(
        database.ConnectionString);

        await IntegrationTestServiceProvider.ApplyMigrationsAsync(
        services,
        cancellationToken);

        const string batchKey = "23.1.test.idempotency.v1";

        // ACT — Primera ejecución.
        int firstRun = await RunBatchAsync(
        services,
        new TestPropertySeedBatch(batchKey),
        cancellationToken);

        Assert.Equal(1, firstRun);

        Guid originalPropertyId;

        // Simular una modificación legítima posterior al seed.
        await using (AsyncServiceScope scope =
        services.CreateAsyncScope())
        {
            BookingDbContext dbContext =
            scope.ServiceProvider.GetRequiredService<
            BookingDbContext>();

            Property property =
            await dbContext.Properties.SingleAsync(
            cancellationToken);

            originalPropertyId = property.Id;

            var renameResult =
            property.Rename("Manually Updated Demo Property");

            Assert.True(renameResult.IsSuccess);

            await dbContext.SaveChangesAsync(
            cancellationToken);
        }

        // ACT — Segunda ejecución con la misma clave.
        int secondRun = await RunBatchAsync(
        services,
        new TestPropertySeedBatch(batchKey),
        cancellationToken);

        // ASSERT — La segunda ejecución fue omitida.
        Assert.Equal(0, secondRun);

        await using (AsyncServiceScope scope =
        services.CreateAsyncScope())
        {
            BookingDbContext dbContext =
            scope.ServiceProvider.GetRequiredService<
            BookingDbContext>();

            Property[] properties =
            await dbContext.Properties
            .AsNoTracking()
            .ToArrayAsync(cancellationToken);

            Property property = Assert.Single(properties);

            Assert.Equal(originalPropertyId, property.Id);

            Assert.Equal(
            "Manually Updated Demo Property",
            property.Name);
        }
    }

    [Fact]
    public async Task FailedBatch_ShouldRollbackAndAllowSafeRetry()
    {
        // ARRANGE
        CancellationToken cancellationToken =
        TestContext.Current.CancellationToken;

        await using var database = new PostgreSqlTestDatabase();
        await database.StartAsync(cancellationToken);

        await using ServiceProvider services =
        IntegrationTestServiceProvider.Create(
        database.ConnectionString);

        await IntegrationTestServiceProvider.ApplyMigrationsAsync(
        services,
        cancellationToken);

        const string batchKey = "23.1.test.rollback.v1";

        // ACT — Escribir dentro de la transacción
        // y después simular un fallo.
        await Assert.ThrowsAsync<InvalidOperationException>(
        () => RunBatchAsync(
        services,
        new TestPropertySeedBatch(
        batchKey,
        failAfterSave: true),
        cancellationToken));

        // ASSERT — La propiedad no quedó persistida.
        await using (AsyncServiceScope scope =
        services.CreateAsyncScope())
        {
            BookingDbContext dbContext =
            scope.ServiceProvider.GetRequiredService<
            BookingDbContext>();

            int count = await dbContext.Properties.CountAsync(
            cancellationToken);

            Assert.Equal(0, count);
        }

        // ACT — Reintentar el mismo lote sin el fallo.
        int retryResult = await RunBatchAsync(
        services,
        new TestPropertySeedBatch(batchKey),
        cancellationToken);

        // ASSERT — El registro de control también fue
        // revertido, permitiendo ejecutar el lote.
        Assert.Equal(1, retryResult);

        await using (AsyncServiceScope scope =
        services.CreateAsyncScope())
        {
            BookingDbContext dbContext =
            scope.ServiceProvider.GetRequiredService<
            BookingDbContext>();

            Assert.Equal(
            1,
            await dbContext.Properties.CountAsync(
            cancellationToken));
        }
    }

    private static async Task<int> RunBatchAsync(
    IServiceProvider services,
    IDemoSeedBatch batch,
    CancellationToken cancellationToken)
    {
        await using AsyncServiceScope scope =
        services.CreateAsyncScope();

        IServiceProvider scopedServices =
        scope.ServiceProvider;

        var runner = new DemoSeedRunner(
        scopedServices.GetRequiredService<
        BookingDbContext>(),
        scopedServices.GetRequiredService<
        ITransactionManager>(),
        scopedServices.GetRequiredService<
        IClock>(),
        [batch],
        NullLogger<DemoSeedRunner>.Instance);

        return await runner.RunAsync(cancellationToken);
    }

    // Solo existe dentro del proyecto de Integration.Tests.
    // No creamos todavía datos demo funcionales en Infrastructure.
    private sealed class TestPropertySeedBatch : IDemoSeedBatch
    {
        private readonly bool _failAfterSave;

        public TestPropertySeedBatch(
        string key,
        bool failAfterSave = false)
        {
            Key = key;
            _failAfterSave = failAfterSave;
        }

        public string Key { get; }

        public async Task ExecuteAsync(
        BookingDbContext dbContext,
        CancellationToken cancellationToken = default)
        {
            var propertyResult = Property.Create(
            "Demo Seed Test Property",
            "America/El_Salvador",
            new TimeOnly(15, 0),
            new TimeOnly(11, 0),
            "demo-test-owner");

            Assert.True(propertyResult.IsSuccess);

            dbContext.Properties.Add(propertyResult.Value);

            if (_failAfterSave)
            {
                // Escribimos realmente en PostgreSQL,
                // todavía dentro de la transacción.
                await dbContext.SaveChangesAsync(
                cancellationToken);

                throw new InvalidOperationException(
                "Simulated seed batch failure.");
            }
        }
    }
}
