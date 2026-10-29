
using Bookify.Services.Booking.Domain.Bookings;
using Bookify.Services.Booking.Domain.Payments;
using Bookify.Services.Booking.Infrastructure.DemoSeeding;
using Bookify.Services.Booking.Infrastructure.Persistence;
using Bookify.Services.Booking.Integration.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using DomainBooking =
    Bookify.Services.Booking.Domain.Bookings.Booking;

namespace Bookify.Services.Booking.Integration.Tests.DemoSeeding;

[Trait("Category", "Integration")]
[Trait("Category", "DemoSeeding")]
public sealed class BookingStatesDemoSeedIntegrationTests
{
    [Fact]
    public async Task Seed_ShouldPersistFiveStatesWithConsistentPayments()
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

        // ==========================================
        // ACT — Primera ejecución
        // ==========================================
        await using (AsyncServiceScope seedScope =
            services.CreateAsyncScope())
        {
            DemoSeedRunner runner =
                seedScope.ServiceProvider
                    .GetRequiredService<DemoSeedRunner>();

            int applied =
                await runner.RunAsync(cancellationToken);

            // Lote 23.2 + lote 23.3.
            Assert.Equal(2, applied);
        }

        // ==========================================
        // ASSERT — Estado persistido
        // ==========================================
        await using (AsyncServiceScope assertionScope =
            services.CreateAsyncScope())
        {
            BookingDbContext dbContext =
                assertionScope.ServiceProvider
                    .GetRequiredService<BookingDbContext>();

            DomainBooking[] bookings =
                await dbContext.Bookings
                    .AsNoTracking()
                    .ToArrayAsync(cancellationToken);

            Assert.Equal(5, bookings.Length);

            // Exactamente una reserva por estado.
            foreach (BookingStatus status in
                Enum.GetValues<BookingStatus>())
            {
                Assert.Single(bookings, booking =>
                        booking.Status == status);
            }

            // Las cinco reservas tienen snapshot
            // y GuestDetails persistidos.
            Assert.All(
                bookings,
                booking =>
                {
                    Assert.NotNull(booking.PriceSnapshot);
                    Assert.NotNull(booking.GuestDetails);

                    Assert.True(
                        booking.PriceSnapshot.TotalPrice.Amount > 0);

                    Assert.Equal(
                        "USD",
                        booking.PriceSnapshot.TotalPrice.Currency);

                    Assert.EndsWith(
                        "@bookify.invalid",
                        booking.GuestDetails.Email, StringComparison.Ordinal);
                });

            // Comprobar PendingApproval.
            DomainBooking pendingApproval =
                Assert.Single(bookings, booking =>
                        booking.Status ==
                            BookingStatus.PendingApproval);

            Assert.NotNull(
                pendingApproval.ApprovalDueAtUtc);

            Assert.Null(
                pendingApproval.ApprovedAtUtc);

            // Comprobar PendingPayment.
            DomainBooking pendingPayment =
                Assert.Single(bookings, booking =>
                        booking.Status ==
                            BookingStatus.PendingPayment);

            Assert.NotNull(
                pendingPayment.ApprovedAtUtc);

            Assert.NotNull(
                pendingPayment.PaymentDueAtUtc);

            // Comprobar Cancelled.
            DomainBooking cancelled =
                Assert.Single(bookings, booking =>
                        booking.Status ==
                            BookingStatus.Cancelled);

            Assert.False(
                cancelled.BlocksInventory);

            Assert.Equal(
                BookingCancellationReason.CancelledByGuest,
                cancelled.CancellationReason);

            Assert.NotNull(
                cancelled.CancelledAtUtc);

            // Las reservas que bloquean inventario.
            Assert.Equal(
                4,
                bookings.Count(booking =>
                    booking.BlocksInventory));

            // ==========================================
            // ASSERT — Payment / Attempt
            // ==========================================
            Payment[] payments =
                await dbContext.Payments
                    .AsNoTracking()
                    .Include(payment => payment.Attempts)
                    .ToArrayAsync(cancellationToken);

            Assert.Equal(2, payments.Length);

            DomainBooking[] paidBookings =
                bookings
                    .Where(booking =>
                        booking.Status is
                            BookingStatus.Paid or
                            BookingStatus.Completed)
                    .ToArray();

            Assert.Equal(2, paidBookings.Length);

            foreach (DomainBooking booking in paidBookings)
            {
                Payment payment =
                    Assert.Single(payments, item =>
                            item.BookingId == booking.Id);

                Assert.Equal(
                    PaymentStatus.Succeeded,
                    payment.Status);

                Assert.Equal(
                    booking.PriceSnapshot!.TotalPrice.Amount,
                    payment.Amount.Amount);

                Assert.Equal(
                    booking.PriceSnapshot.TotalPrice.Currency,
                    payment.Amount.Currency);

                Assert.Equal(
                    booking.PaidAtUtc,
                    payment.CompletedAtUtc);

                PaymentAttempt attempt =
                    Assert.Single(payment.Attempts);

                Assert.Equal(
                    PaymentAttemptStatus.Succeeded,
                    attempt.Status);

                Assert.Equal(
                    payment.Id,
                    attempt.PaymentId);

                Assert.Equal(
                    payment.Amount.Amount,
                    attempt.Amount.Amount);

                Assert.Equal(
                    payment.Amount.Currency,
                    attempt.Amount.Currency);

                Assert.Equal(
                    payment.CompletedAtUtc,
                    attempt.CompletedAtUtc);
            }

            DomainBooking completed =
                Assert.Single(bookings, booking =>
                        booking.Status ==
                            BookingStatus.Completed);

            Assert.NotNull(
                completed.CompletedAtUtc);

            Assert.True(
                completed.CompletedAtUtc >
                completed.PaidAtUtc);
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

            // Ninguno de los dos lotes debe repetirse.
            Assert.Equal(0, applied);
        }

        // ==========================================
        // ASSERT — Sin duplicados
        // ==========================================
        await using (AsyncServiceScope finalScope =
            services.CreateAsyncScope())
        {
            BookingDbContext dbContext =
                finalScope.ServiceProvider
                    .GetRequiredService<BookingDbContext>();

            Assert.Equal(
                5,
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
        }
    }
}
