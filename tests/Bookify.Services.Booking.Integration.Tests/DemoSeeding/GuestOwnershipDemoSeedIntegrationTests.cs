
using Bookify.Services.Booking.Domain.Bookings;
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
public sealed class GuestOwnershipDemoSeedIntegrationTests
{
    [Fact]
    public async Task Seed_ShouldPreserveGuestOwnershipAndCreateAuthenticatedCustomerBooking()
    {
        // ARRANGE
        CancellationToken cancellationToken =
            TestContext.Current.CancellationToken;

        await using var database =
            new PostgreSqlTestDatabase();

        await database.StartAsync(cancellationToken);

        const string ownerSubjectId =
            "demo-test-owner-subject";

        const string customerSubjectId =
            "demo-test-customer-subject";

        IConfiguration configuration =
            new ConfigurationBuilder()
                .AddInMemoryCollection(
                    new Dictionary<string, string?>
                    {
                        ["Payments:Provider"] = "Fake",
                        ["DemoSeed:OwnerSubjectId"] =
                            ownerSubjectId,
                        ["DemoSeed:CustomerSubjectId"] =
                            customerSubjectId
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
        // ACT — Ejecutar los tres lotes.
        // ==========================================
        await using (AsyncServiceScope seedScope =
            services.CreateAsyncScope())
        {
            DemoSeedRunner runner =
                seedScope.ServiceProvider
                    .GetRequiredService<DemoSeedRunner>();

            int appliedBatches =
                await runner.RunAsync(cancellationToken);

            Assert.Equal(3, appliedBatches);
        }

        // ==========================================
        // ASSERT — Recuperar datos de PostgreSQL.
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

            Assert.Equal(6, bookings.Length);

            // ======================================
            // A. Las cinco reservas originales
            //    siguen siendo Guest.
            // ======================================
            DomainBooking[] guestBookings =
                bookings
                    .Where(booking =>
                        booking.CustomerSubjectId is null)
                    .ToArray();

            Assert.Equal(5, guestBookings.Length);

            Assert.All(
                guestBookings,
                booking =>
                {
                    Assert.NotNull(booking.GuestDetails);

                    Assert.EndsWith(
                        "@bookify.invalid",
                        booking.GuestDetails.Email, StringComparison.Ordinal);

                    Assert.NotNull(
                        booking.GuestAccessTokenHash);

                    Assert.Matches(
                        "^[0-9A-F]{64}$",
                        booking.GuestAccessTokenHash);

                    // El token original no se recupera
                    // al volver a leer la reserva.
                    Assert.Null(
                        booking.TakeGuestAccessToken());

                    Assert.False(
                        booking.VerifyGuestAccessToken(
                            new string('x', 43)));
                });

            // ======================================
            // B. Existe exactamente una reserva
            //    vinculada al Customer autenticado.
            // ======================================
            DomainBooking customerBooking =
                Assert.Single(bookings, booking =>
                        booking.CustomerSubjectId ==
                            customerSubjectId);

            Assert.Equal(
                BookingStatus.PendingApproval,
                customerBooking.Status);

            Assert.Equal(
                customerSubjectId,
                customerBooking.CustomerSubjectId);

            // No debe existir una segunda modalidad
            // de acceso para la misma reserva.
            Assert.Null(
                customerBooking.GuestAccessTokenHash);

            Assert.Null(
                customerBooking.TakeGuestAccessToken());

            Assert.False(
                customerBooking.VerifyGuestAccessToken(
                    new string('x', 43)));

            // ======================================
            // C. Los datos de contacto son ficticios.
            // ======================================
            Assert.NotNull(
                customerBooking.GuestDetails);

            Assert.Equal(
                GuestOwnershipDemoSeedBatch.CustomerDemoEmail,
                customerBooking.GuestDetails.Email);

            Assert.Equal(
                "Cliente Demo Autenticado",
                customerBooking.GuestDetails.FullName);

            Assert.Equal(
                "+12025550106",
                customerBooking.GuestDetails.Phone);

            // ======================================
            // D. El ownership de la Property
            //    sigue siendo el mismo.
            // ======================================
            var property =
                await dbContext.Properties
                    .AsNoTracking()
                    .SingleAsync(cancellationToken);

            Assert.Equal(
                ownerSubjectId,
                property.OwnerSubjectId);

            Assert.Equal(
                property.Id,
                customerBooking.PropertyId);

            DomainBooking paidGuestBooking =
                Assert.Single(guestBookings, booking =>
                        booking.GuestDetails!.Email ==
                            BookingStatesDemoSeedBatch.PaidDemoEmail);

            // La reserva Customer usa la misma unidad
            // que una reserva Guest real del seed 23.3.
            Assert.Equal(
                paidGuestBooking.RentableUnitId,
                customerBooking.RentableUnitId);

            Assert.False(
                customerBooking.StayPeriod.Overlaps(
                    paidGuestBooking.StayPeriod));

            // ======================================
            // E. Pricing persistido.
            // ======================================
            Assert.NotNull(
                customerBooking.PriceSnapshot);

            Assert.True(
                customerBooking.PriceSnapshot.TotalPrice.Amount > 0);

            Assert.Equal(
                "USD",
                customerBooking.PriceSnapshot.TotalPrice.Currency);

            // ======================================
            // F. No se añadió un Payment ficticio
            //    para PendingApproval.
            // ======================================
            Assert.Equal(
                2,
                await dbContext.Payments.CountAsync(
                    cancellationToken));

            Assert.Equal(
                2,
                await dbContext.PaymentAttempts.CountAsync(
                    cancellationToken));
        }

        // ==========================================
        // ACT — Segunda ejecución del runner.
        // ==========================================
        await using (AsyncServiceScope retryScope =
            services.CreateAsyncScope())
        {
            DemoSeedRunner runner =
                retryScope.ServiceProvider
                    .GetRequiredService<DemoSeedRunner>();

            int appliedBatches =
                await runner.RunAsync(cancellationToken);

            Assert.Equal(0, appliedBatches);
        }

        // ==========================================
        // ASSERT — No se duplicaron las reservas.
        // ==========================================
        await using (AsyncServiceScope finalScope =
            services.CreateAsyncScope())
        {
            BookingDbContext dbContext =
                finalScope.ServiceProvider
                    .GetRequiredService<BookingDbContext>();

            Assert.Equal(
                6,
                await dbContext.Bookings.CountAsync(
                    cancellationToken));

            Assert.Equal(
                1,
                await dbContext.Bookings.CountAsync(
                    booking =>
                        booking.CustomerSubjectId ==
                            customerSubjectId,
                    cancellationToken));
        }
    }
}
