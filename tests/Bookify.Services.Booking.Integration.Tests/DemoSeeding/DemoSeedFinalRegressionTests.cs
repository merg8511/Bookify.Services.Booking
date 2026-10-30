
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
public sealed class DemoSeedFinalRegressionTests
{
    private const string OwnerSubjectId =
        "demo-regression-owner";

    private const string CustomerSubjectId =
        "demo-regression-customer";

    // ==========================================
    // TEST 1 — Concurrencia del seed
    // ==========================================
    [Fact]
    public async Task ConcurrentRunners_ShouldApplyEachBatchExactlyOnce()
    {
        // ARRANGE
        CancellationToken cancellationToken =
            TestContext.Current.CancellationToken;

        await using var database =
            new PostgreSqlTestDatabase();

        await database.StartAsync(cancellationToken);

        await using ServiceProvider services =
            IntegrationTestServiceProvider.Create(
                database.ConnectionString,
                CreateConfiguration());

        await IntegrationTestServiceProvider
            .ApplyMigrationsAsync(
                services,
                cancellationToken);

        // Cada invocación tiene su propio scope y DbContext.
        // Ambas compiten sobre el mismo PostgreSQL.
        async Task<int> RunIndependentScopeAsync()
        {
            await using AsyncServiceScope scope =
                services.CreateAsyncScope();

            DemoSeedRunner runner =
                scope.ServiceProvider
                    .GetRequiredService<DemoSeedRunner>();

            return await runner.RunAsync(cancellationToken);
        }

        // ACT
        Task<int> first = RunIndependentScopeAsync();
        Task<int> second = RunIndependentScopeAsync();

        int[] results =
            await Task.WhenAll(first, second);

        // ASSERT
        // No exigimos qué runner ganó cada lote.
        // Solo exigimos que cada lote se aplicó una vez.
        Assert.Equal(4, results.Sum());

        await using AsyncServiceScope verifyScope =
            services.CreateAsyncScope();

        BookingDbContext dbContext =
            verifyScope.ServiceProvider
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

        Assert.Equal(
            2,
            await dbContext.PaymentAttempts.CountAsync(
                cancellationToken));

        // Cinco Guest del 23.3 + cuatro Guest del 23.6.
        Assert.Equal(
            9,
            await dbContext.Bookings.CountAsync(
                booking =>
                    booking.CustomerSubjectId == null,
                cancellationToken));

        // Una reserva Customer del 23.4.
        Assert.Equal(
            1,
            await dbContext.Bookings.CountAsync(
                booking =>
                    booking.CustomerSubjectId ==
                        CustomerSubjectId,
                cancellationToken));

        // La auditoría de 23.5 debe continuar pasando.
        DemoPaymentConsistencyValidator validator =
            verifyScope.ServiceProvider
                .GetRequiredService<
                    DemoPaymentConsistencyValidator>();

        await validator.ValidateAsync(cancellationToken);

        // Una tercera ejecución ya no aplica lotes.
        await using AsyncServiceScope retryScope =
            services.CreateAsyncScope();

        DemoSeedRunner retryRunner =
            retryScope.ServiceProvider
                .GetRequiredService<DemoSeedRunner>();

        Assert.Equal(
            0,
            await retryRunner.RunAsync(cancellationToken));
    }

    // ==========================================
    // TEST 2 — Preservación de datos existentes
    // ==========================================
    [Fact]
    public async Task Reexecution_ShouldNotOverwriteModifiedOrUnrelatedData()
    {
        // ARRANGE
        CancellationToken cancellationToken =
            TestContext.Current.CancellationToken;

        await using var database =
            new PostgreSqlTestDatabase();

        await database.StartAsync(cancellationToken);

        await using ServiceProvider services =
            IntegrationTestServiceProvider.Create(
                database.ConnectionString,
                CreateConfiguration());

        await IntegrationTestServiceProvider
            .ApplyMigrationsAsync(
                services,
                cancellationToken);

        await RunSeedAsync(services, cancellationToken);

        Guid demoPropertyId;
        Guid demoRoomId;
        Guid unrelatedPropertyId;

        const string modifiedPropertyName =
            "Demo Property Manually Renamed";

        const string modifiedRoomName =
            "Demo Room Manually Renamed";

        // ACT — Modificar datos después del primer seed.
        await using (AsyncServiceScope modificationScope =
            services.CreateAsyncScope())
        {
            BookingDbContext dbContext =
                modificationScope.ServiceProvider
                    .GetRequiredService<BookingDbContext>();

            Property property =
                await dbContext.Properties.SingleAsync(
                    item =>
                        item.OwnerSubjectId == OwnerSubjectId,
                    cancellationToken);

            demoPropertyId = property.Id;

            Assert.True(
                property.Rename(modifiedPropertyName).IsSuccess);

            RentableUnit room =
                await dbContext.RentableUnits
                    .Where(item =>
                        item.PropertyId == property.Id &&
                        item.Type == RentableUnitType.Room)
                    .OrderBy(item => item.Name)
                    .FirstAsync(cancellationToken);

            demoRoomId = room.Id;

            Assert.True(
                room.Rename(modifiedRoomName).IsSuccess);

            // Crear una propiedad ajena a los datos Demo.
            Property unrelatedProperty =
                Property.Create(
                    "Independent User Property",
                    "America/El_Salvador",
                    new TimeOnly(15, 0),
                    new TimeOnly(11, 0),
                    "owner-outside-demo").Value;

            unrelatedPropertyId = unrelatedProperty.Id;

            dbContext.Properties.Add(unrelatedProperty);

            await dbContext.SaveChangesAsync(
                cancellationToken);
        }

        // ACT — Repetir el seed completo.
        await using (AsyncServiceScope retryScope =
            services.CreateAsyncScope())
        {
            DemoSeedRunner runner =
                retryScope.ServiceProvider
                    .GetRequiredService<DemoSeedRunner>();

            int applied =
                await runner.RunAsync(cancellationToken);

            Assert.Equal(0, applied);
        }

        // ASSERT — No hubo restauración ni duplicación.
        await using AsyncServiceScope verifyScope =
            services.CreateAsyncScope();

        BookingDbContext verificationDb =
            verifyScope.ServiceProvider
                .GetRequiredService<BookingDbContext>();

        Property persistedProperty =
            await verificationDb.Properties
                .AsNoTracking()
                .SingleAsync(
                    property =>
                        property.Id == demoPropertyId,
                    cancellationToken);

        Assert.Equal(
            modifiedPropertyName,
            persistedProperty.Name);

        RentableUnit persistedRoom =
            await verificationDb.RentableUnits
                .AsNoTracking()
                .SingleAsync(
                    unit =>
                        unit.Id == demoRoomId,
                    cancellationToken);

        Assert.Equal(
            modifiedRoomName,
            persistedRoom.Name);

        Property persistedUnrelatedProperty =
            await verificationDb.Properties
                .AsNoTracking()
                .SingleAsync(
                    property =>
                        property.Id == unrelatedPropertyId,
                    cancellationToken);

        Assert.Equal(
            "Independent User Property",
            persistedUnrelatedProperty.Name);

        Assert.Equal(
            2,
            await verificationDb.Properties.CountAsync(
                cancellationToken));

        Assert.Equal(
            4,
            await verificationDb.RentableUnits.CountAsync(
                cancellationToken));

        Assert.Equal(
            10,
            await verificationDb.Bookings.CountAsync(
                cancellationToken));

        // La modificación de nombres no debe afectar
        // las relaciones Payment/Booking.
        DemoPaymentConsistencyValidator validator =
            verifyScope.ServiceProvider
                .GetRequiredService<
                    DemoPaymentConsistencyValidator>();

        await validator.ValidateAsync(cancellationToken);
    }

    // ==========================================
    // TEST 3 — Corrupción de Payment y Attempt
    // ==========================================
    [Fact]
    public async Task PersistedPaymentCorruption_ShouldBeDetected()
    {
        // ARRANGE
        CancellationToken cancellationToken =
            TestContext.Current.CancellationToken;

        await using var database =
            new PostgreSqlTestDatabase();

        await database.StartAsync(cancellationToken);

        await using ServiceProvider services =
            IntegrationTestServiceProvider.Create(
                database.ConnectionString,
                CreateConfiguration());

        await IntegrationTestServiceProvider
            .ApplyMigrationsAsync(
                services,
                cancellationToken);

        await RunSeedAsync(services, cancellationToken);

        Guid paymentId;
        decimal originalAmount;

        await using (AsyncServiceScope lookupScope =
            services.CreateAsyncScope())
        {
            BookingDbContext dbContext =
                lookupScope.ServiceProvider
                    .GetRequiredService<BookingDbContext>();

            Guid paidBookingId =
                await dbContext.Bookings
                    .AsNoTracking()
                    .Where(booking =>
                        booking.GuestDetails != null &&
                        booking.GuestDetails.Email ==
                            BookingStatesDemoSeedBatch.PaidDemoEmail)
                    .Select(booking => booking.Id)
                    .SingleAsync(cancellationToken);

            var payment =
                await dbContext.Payments
                    .AsNoTracking()
                    .SingleAsync(
                        item =>
                            item.BookingId == paidBookingId,
                        cancellationToken);

            paymentId = payment.Id;
            originalAmount = payment.Amount.Amount;
        }

        // Confirmar que el dataset original es válido.
        await ValidatePaymentsAsync(
            services,
            cancellationToken);

        // ==========================================
        // A. Modificar el importe directamente en SQL.
        // ==========================================
        // Solo ocurre en el PostgreSQL descartable del test.
        await using (AsyncServiceScope mutationScope =
            services.CreateAsyncScope())
        {
            BookingDbContext dbContext =
                mutationScope.ServiceProvider
                    .GetRequiredService<BookingDbContext>();

            int updated =
                await dbContext.Database
                    .ExecuteSqlInterpolatedAsync(
                        $"""
                        UPDATE payments
                        SET amount = amount + 1
                        WHERE id = {paymentId}
                        """,
                        cancellationToken);

            Assert.Equal(1, updated);
        }

        // ACT / ASSERT — El validador rechaza el importe.
        await using (AsyncServiceScope validationScope =
            services.CreateAsyncScope())
        {
            DemoPaymentConsistencyValidator validator =
                validationScope.ServiceProvider
                    .GetRequiredService<
                        DemoPaymentConsistencyValidator>();

            InvalidOperationException exception =
                await Assert.ThrowsAsync<
                    InvalidOperationException>(
                    () => validator.ValidateAsync(
                        cancellationToken));

            Assert.Contains(
                "amount or currency",
                exception.Message, StringComparison.Ordinal);
        }

        // ==========================================
        // B. Restaurar el importe original.
        // ==========================================
        await using (AsyncServiceScope restoreScope =
            services.CreateAsyncScope())
        {
            BookingDbContext dbContext =
                restoreScope.ServiceProvider
                    .GetRequiredService<BookingDbContext>();

            int updated =
                await dbContext.Database
                    .ExecuteSqlInterpolatedAsync(
                        $"""
                        UPDATE payments
                        SET amount = {originalAmount}
                        WHERE id = {paymentId}
                        """,
                        cancellationToken);

            Assert.Equal(1, updated);
        }

        // Debe volver a ser válido.
        await ValidatePaymentsAsync(
            services,
            cancellationToken);

        // ==========================================
        // C. Eliminar el PaymentAttempt de prueba.
        // ==========================================
        await using (AsyncServiceScope mutationScope =
            services.CreateAsyncScope())
        {
            BookingDbContext dbContext =
                mutationScope.ServiceProvider
                    .GetRequiredService<BookingDbContext>();

            int deleted =
                await dbContext.Database
                    .ExecuteSqlInterpolatedAsync(
                        $"""
                        DELETE FROM payment_attempts
                        WHERE payment_id = {paymentId}
                        """,
                        cancellationToken);

            Assert.Equal(1, deleted);
        }

        // ACT / ASSERT — Detectar Attempt inexistente.
        await using (AsyncServiceScope validationScope =
            services.CreateAsyncScope())
        {
            DemoPaymentConsistencyValidator validator =
                validationScope.ServiceProvider
                    .GetRequiredService<
                        DemoPaymentConsistencyValidator>();

            InvalidOperationException exception =
                await Assert.ThrowsAsync<
                    InvalidOperationException>(
                    () => validator.ValidateAsync(
                        cancellationToken));

            Assert.Contains(
                "PaymentAttempt",
                exception.Message, StringComparison.Ordinal);
        }
    }

    // ==========================================
    // Helpers de prueba — Reutilizan infraestructura
    // ==========================================
    private static IConfiguration CreateConfiguration()
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["Payments:Provider"] = "Fake",
                    ["DemoSeed:OwnerSubjectId"] =
                        OwnerSubjectId,
                    ["DemoSeed:CustomerSubjectId"] =
                        CustomerSubjectId
                })
            .Build();
    }

    private static async Task RunSeedAsync(
        IServiceProvider services,
        CancellationToken cancellationToken)
    {
        await using AsyncServiceScope scope =
            services.CreateAsyncScope();

        DemoSeedRunner runner =
            scope.ServiceProvider
                .GetRequiredService<DemoSeedRunner>();

        Assert.Equal(
            4,
            await runner.RunAsync(cancellationToken));
    }

    private static async Task ValidatePaymentsAsync(
        IServiceProvider services,
        CancellationToken cancellationToken)
    {
        await using AsyncServiceScope scope =
            services.CreateAsyncScope();

        DemoPaymentConsistencyValidator validator =
            scope.ServiceProvider
                .GetRequiredService<
                    DemoPaymentConsistencyValidator>();

        await validator.ValidateAsync(cancellationToken);
    }
}
