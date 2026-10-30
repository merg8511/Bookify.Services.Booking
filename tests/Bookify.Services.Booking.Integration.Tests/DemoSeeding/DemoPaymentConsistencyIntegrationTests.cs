
using Bookify.Services.Booking.Domain.Payments;
using Bookify.Services.Booking.Infrastructure.DemoSeeding;
using Bookify.Services.Booking.Infrastructure.Persistence;
using Bookify.Services.Booking.Integration.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Bookify.Services.Booking.Integration.Tests.DemoSeeding;

[Trait("Category", "Integration")]
[Trait("Category", "DemoSeeding")]
public sealed class DemoPaymentConsistencyIntegrationTests
{
    [Fact]
    public async Task ValidSeedPayments_ShouldPassConsistencyValidation()
    {
        // ARRANGE
        CancellationToken cancellationToken =
            TestContext.Current.CancellationToken;

        await using var database =
            new PostgreSqlTestDatabase();

        await database.StartAsync(cancellationToken);

        // Incluimos el Customer de 23.4 para verificar
        // que el auditor funciona con el dataset acumulado.
        IConfiguration configuration =
            CreateConfiguration(
                includeCustomer: true);

        await using ServiceProvider services =
            IntegrationTestServiceProvider.Create(
                database.ConnectionString,
                configuration);

        await IntegrationTestServiceProvider
            .ApplyMigrationsAsync(
                services,
                cancellationToken);

        // ==========================================
        // ACT — Primera ejecución del seed
        // ==========================================
        await using (AsyncServiceScope seedScope =
            services.CreateAsyncScope())
        {
            DemoSeedRunner runner =
                seedScope.ServiceProvider
                    .GetRequiredService<DemoSeedRunner>();

            int applied =
                await runner.RunAsync(cancellationToken);

            // Lotes 23.2 + 23.3 + 23.4.
            Assert.Equal(4, applied);
        }

        // ==========================================
        // ASSERT — Validar datos persistidos
        // ==========================================
        await using (AsyncServiceScope validationScope =
            services.CreateAsyncScope())
        {
            DemoPaymentConsistencyValidator validator =
                validationScope.ServiceProvider
                    .GetRequiredService<
                        DemoPaymentConsistencyValidator>();

            // Si algún vínculo, importe, estado o
            // timestamp no coincide, lanzará excepción.
            await validator.ValidateAsync(
                cancellationToken);

            BookingDbContext dbContext =
                validationScope.ServiceProvider
                    .GetRequiredService<BookingDbContext>();

            // El dataset acumulado incluye seis reservas.
            Assert.Equal(
            10,
            await dbContext.Bookings.CountAsync(
                cancellationToken));

            Payment[] payments =
                await dbContext.Payments
                    .AsNoTracking()
                    .Include(payment => payment.Attempts)
                    .ToArrayAsync(cancellationToken);

            Assert.Equal(2, payments.Length);

            Assert.All(
                payments,
                payment =>
                {
                    Assert.Equal(
                        PaymentStatus.Succeeded,
                        payment.Status);

                    PaymentAttempt attempt =
                        Assert.Single(payment.Attempts);

                    Assert.Equal(
                        PaymentAttemptStatus.Succeeded,
                        attempt.Status);

                    Assert.Equal(
                        payment.Id,
                        attempt.PaymentId);
                });
        }

        // ==========================================
        // ACT — Segunda ejecución
        // ==========================================
        await using (AsyncServiceScope retryScope =
            services.CreateAsyncScope())
        {
            DemoSeedRunner runner =
                retryScope.ServiceProvider
                    .GetRequiredService<DemoSeedRunner>();

            int applied =
                await runner.RunAsync(cancellationToken);

            // La validación no necesita crear
            // un nuevo lote en el ledger.
            Assert.Equal(0, applied);

            DemoPaymentConsistencyValidator validator =
                retryScope.ServiceProvider
                    .GetRequiredService<
                        DemoPaymentConsistencyValidator>();

            await validator.ValidateAsync(
                cancellationToken);
        }
    }

    [Fact]
    public async Task MissingPayment_ShouldFailValidationAfterSeedWasApplied()
    {
        // ARRANGE
        CancellationToken cancellationToken =
            TestContext.Current.CancellationToken;

        await using var database =
            new PostgreSqlTestDatabase();

        await database.StartAsync(cancellationToken);

        // En este test no registramos 23.4.
        // Los lotes 23.2 y 23.3 son suficientes.
        IConfiguration configuration =
            CreateConfiguration(
                includeCustomer: false);

        await using ServiceProvider services =
            IntegrationTestServiceProvider.Create(
                database.ConnectionString,
                configuration);

        await IntegrationTestServiceProvider
            .ApplyMigrationsAsync(
                services,
                cancellationToken);

        await using (AsyncServiceScope seedScope =
            services.CreateAsyncScope())
        {
            DemoSeedRunner runner =
                seedScope.ServiceProvider
                    .GetRequiredService<DemoSeedRunner>();

            Assert.Equal(
            3,
            await runner.RunAsync(cancellationToken));
        }

        // ==========================================
        // ARRANGE — Simular corrupción de datos
        // ==========================================
        // Solamente modificamos la base descartable
        // de Testcontainers, nunca datos reales.
        Guid paidBookingId;

        await using (AsyncServiceScope lookupScope =
            services.CreateAsyncScope())
        {
            BookingDbContext dbContext =
                lookupScope.ServiceProvider
                    .GetRequiredService<BookingDbContext>();

            paidBookingId =
                await dbContext.Bookings
                    .AsNoTracking()
                    .Where(booking =>
                        booking.GuestDetails != null &&
                        booking.GuestDetails.Email ==
                            BookingStatesDemoSeedBatch.PaidDemoEmail)
                    .Select(booking => booking.Id)
                    .SingleAsync(cancellationToken);
        }

        await using (AsyncServiceScope modificationScope =
            services.CreateAsyncScope())
        {
            BookingDbContext dbContext =
                modificationScope.ServiceProvider
                    .GetRequiredService<BookingDbContext>();

            Payment payment =
                await dbContext.Payments
                    .Include(item => item.Attempts)
                    .SingleAsync(
                        item =>
                            item.BookingId == paidBookingId,
                        cancellationToken);

            // Eliminar el Payment elimina también sus
            // attempts según la relación EF existente.
            dbContext.Payments.Remove(payment);

            await dbContext.SaveChangesAsync(
                cancellationToken);
        }

        // ==========================================
        // ACT — El runner no repara datos existentes
        // ==========================================
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

        // ==========================================
        // ASSERT — Detectar la inconsistencia
        // ==========================================
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
                "Demo payment consistency failed",
                exception.Message, StringComparison.Ordinal);
        }
    }

    private static IConfiguration CreateConfiguration(
        bool includeCustomer)
    {
        var settings =
            new Dictionary<string, string?>
            {
                ["Payments:Provider"] = "Fake",
                ["DemoSeed:OwnerSubjectId"] =
                    "demo-owner-subject"
            };

        if (includeCustomer)
        {
            settings["DemoSeed:CustomerSubjectId"] =
                "demo-customer-subject";
        }

        return new ConfigurationBuilder()
            .AddInMemoryCollection(settings)
            .Build();
    }
}
