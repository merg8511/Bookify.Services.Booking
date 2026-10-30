
using Bookify.Services.Booking.Application.Availability;
using Bookify.Services.Booking.Application.Availability.Get;
using Bookify.Services.Booking.Application.Properties;
using Bookify.Services.Booking.Domain.Bookings;
using Bookify.Services.Booking.Domain.Properties;
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
public sealed class AvailabilityConflictsDemoSeedIntegrationTests
{
    [Fact]
    public async Task Seed_ShouldCreateReproducibleAvailabilityAndPricingScenarios()
    {
        // ==========================================
        // ARRANGE — PostgreSQL real
        // ==========================================
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
                            "demo-owner-subject",
                        ["DemoSeed:CustomerSubjectId"] =
                            "demo-customer-subject"
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
        // ACT — Ejecutar los lotes acumulados
        // ==========================================
        await using (AsyncServiceScope seedScope =
            services.CreateAsyncScope())
        {
            DemoSeedRunner runner =
                seedScope.ServiceProvider
                    .GetRequiredService<DemoSeedRunner>();

            int appliedBatches =
                await runner.RunAsync(cancellationToken);

            // 23.2, 23.3, 23.4 y 23.6.
            // 23.5 es un validador, no un lote.
            Assert.Equal(4, appliedBatches);

            DemoPaymentConsistencyValidator validator =
                seedScope.ServiceProvider
                    .GetRequiredService<
                        DemoPaymentConsistencyValidator>();

            await validator.ValidateAsync(
                cancellationToken);
        }

        // ==========================================
        // ASSERT — Consultar PostgreSQL
        // ==========================================
        await using (AsyncServiceScope assertionScope =
            services.CreateAsyncScope())
        {
            IServiceProvider scopedServices =
                assertionScope.ServiceProvider;

            BookingDbContext dbContext =
                scopedServices
                    .GetRequiredService<BookingDbContext>();

            IAvailabilityReadService availability =
                scopedServices
                    .GetRequiredService<IAvailabilityReadService>();

            IPropertyReadService propertyReader =
                scopedServices
                    .GetRequiredService<IPropertyReadService>();

            DomainBooking[] bookings =
                await dbContext.Bookings
                    .AsNoTracking()
                    .ToArrayAsync(cancellationToken);

            // 5 de 23.3 + 1 de 23.4 + 4 de 23.6.
            Assert.Equal(10, bookings.Length);

            DomainBooking roomConflict = FindBooking(
                bookings,
                AvailabilityConflictsDemoSeedBatch
                    .RoomConflictEmail);

            DomainBooking entireConflict = FindBooking(
                bookings,
                AvailabilityConflictsDemoSeedBatch
                    .EntirePropertyConflictEmail);

            DomainBooking cancelled = FindBooking(
                bookings,
                AvailabilityConflictsDemoSeedBatch
                    .CancelledEmail);

            DomainBooking seasonal = FindBooking(
                bookings,
                AvailabilityConflictsDemoSeedBatch
                    .SeasonalPriceEmail);

            Guid propertyId = roomConflict.PropertyId;

            RentableUnit[] units =
                await dbContext.RentableUnits
                    .AsNoTracking()
                    .Include(unit => unit.Pricing)
                    .Include(unit => unit.PricingSeasons)
                    .Where(unit =>
                        unit.PropertyId == propertyId)
                    .ToArrayAsync(cancellationToken);

            Assert.Equal(4, units.Length);

            RentableUnit entireProperty =
                Assert.Single(units, unit =>
                    unit.Type ==
                        RentableUnitType.EntireProperty);

            // ==========================================
            // A. Room conflict
            // ==========================================
            Assert.Equal(
                BookingStatus.PendingApproval,
                roomConflict.Status);

            IReadOnlyList<
                Bookify.Services.Booking.Application.Availability
                    .ReadModels.AvailableRentableUnitCandidateReadModel>
                availableDuringRoom =
                    await availability.GetAvailableUnitsAsync(
                        propertyId,
                        roomConflict.StayPeriod.CheckInDate,
                        roomConflict.StayPeriod.CheckOutDate,
                        guestCount: 2,
                        cancellationToken);

            Guid[] availableRoomIds =
                availableDuringRoom
                    .Select(unit => unit.Id)
                    .ToArray();

            // La Room reservada no está disponible.
            Assert.DoesNotContain(
                roomConflict.RentableUnitId,
                availableRoomIds);

            // EntireProperty tampoco puede reservarse.
            Assert.DoesNotContain(
                entireProperty.Id,
                availableRoomIds);

            // Las otras dos Rooms sí están disponibles.
            Assert.Equal(2, availableRoomIds.Length);

            var conflictsForEntire =
                await availability.GetInventoryConflictsAsync(
                    propertyId,
                    entireProperty.Id,
                    roomConflict.StayPeriod.CheckInDate,
                    roomConflict.StayPeriod.CheckOutDate,
                    cancellationToken);

            Assert.Contains(
                roomConflict.Id,
                conflictsForEntire
                    .Select(item => item.BookingId));

            // ==========================================
            // B. EntireProperty conflict
            // ==========================================
            Assert.Equal(
                BookingStatus.PendingApproval,
                entireConflict.Status);

            Assert.Equal(
                entireProperty.Id,
                entireConflict.RentableUnitId);

            var availableDuringEntire =
                await availability.GetAvailableUnitsAsync(
                    propertyId,
                    entireConflict.StayPeriod.CheckInDate,
                    entireConflict.StayPeriod.CheckOutDate,
                    guestCount: 2,
                    cancellationToken);

            // EntireProperty bloquea las tres Rooms.
            Assert.Empty(availableDuringEntire);

            foreach (RentableUnit room in
                units.Where(unit =>
                    unit.Type == RentableUnitType.Room))
            {
                var roomConflicts =
                    await availability.GetInventoryConflictsAsync(
                        propertyId,
                        room.Id,
                        entireConflict.StayPeriod.CheckInDate,
                        entireConflict.StayPeriod.CheckOutDate,
                        cancellationToken);

                Assert.Contains(
                    entireConflict.Id,
                    roomConflicts
                        .Select(item => item.BookingId));
            }

            // ==========================================
            // C. Cancelled no bloquea inventario
            // ==========================================
            Assert.Equal(
                BookingStatus.Cancelled,
                cancelled.Status);

            Assert.False(cancelled.BlocksInventory);

            Assert.Equal(
                BookingCancellationReason.CancelledByGuest,
                cancelled.CancellationReason);

            var availableDuringCancelled =
                await availability.GetAvailableUnitsAsync(
                    propertyId,
                    cancelled.StayPeriod.CheckInDate,
                    cancelled.StayPeriod.CheckOutDate,
                    guestCount: 2,
                    cancellationToken);

            // Las cuatro unidades están disponibles.
            Assert.Equal(4, availableDuringCancelled.Count);

            Assert.Contains(
                cancelled.RentableUnitId,
                availableDuringCancelled
                    .Select(unit => unit.Id));

            Assert.Contains(
                entireProperty.Id,
                availableDuringCancelled
                    .Select(unit => unit.Id));

            var cancelledRoomConflicts =
                await availability.GetInventoryConflictsAsync(
                    propertyId,
                    cancelled.RentableUnitId,
                    cancelled.StayPeriod.CheckInDate,
                    cancelled.StayPeriod.CheckOutDate,
                    cancellationToken);

            // La reserva cancelada no se reporta
            // como conflicto de disponibilidad.
            Assert.DoesNotContain(
                cancelled.Id,
                cancelledRoomConflicts
                    .Select(item => item.BookingId));

            Assert.Empty(cancelledRoomConflicts);

            // ==========================================
            // D. Precio de temporada — prioridad 20
            // ==========================================
            Assert.Equal(
                BookingStatus.PendingApproval,
                seasonal.Status);

            Assert.Equal(
                1,
                seasonal.StayPeriod.NumberOfNights);

            RentableUnit seasonalRoom =
                units.Single(unit =>
                    unit.Id == seasonal.RentableUnitId);

            var specialSeason =
                Assert.Single(
                    seasonalRoom.PricingSeasons,
                        season =>
                            season.IsActive &&
                            season.Priority == 20);

            Assert.NotNull(seasonal.PriceSnapshot);

            Assert.Equal(
                specialSeason.NightlyRate.Amount,
                seasonal.PriceSnapshot
                    .AccommodationPrice.Amount);

            Assert.Equal(
                "USD",
                seasonal.PriceSnapshot.TotalPrice.Currency);

            // La Room de la reserva estacional
            // no se ofrece de nuevo en esas fechas.
            var availableDuringSeason =
                await availability.GetAvailableUnitsAsync(
                    propertyId,
                    seasonal.StayPeriod.CheckInDate,
                    seasonal.StayPeriod.CheckOutDate,
                    guestCount: 2,
                    cancellationToken);

            Assert.DoesNotContain(
                seasonal.RentableUnitId,
                availableDuringSeason
                    .Select(unit => unit.Id));

            // ==========================================
            // E. Consultar pricing por el QueryHandler real
            // ==========================================
            var handler =
                new GetAvailabilityQueryHandler(
                    propertyReader,
                    availability);

            var queryResult =
                await handler.HandleAsync(
                    new GetAvailabilityQuery(
                        propertyId,
                        seasonal.StayPeriod.CheckInDate,
                        seasonal.StayPeriod.CheckOutDate,
                        2),
                    cancellationToken);

            Assert.True(queryResult.IsSuccess);

            // Las habitaciones libres también tienen
            // la temporada de prioridad 20 de 23.2.
            var quotedRooms =
                queryResult.Value.AvailableUnits
                    .Where(unit =>
                        unit.Type == "Room")
                    .ToArray();

            Assert.NotEmpty(quotedRooms);

            foreach (var quotedRoom in quotedRooms)
            {
                RentableUnit unit =
                    units.Single(item =>
                        item.Id == quotedRoom.Id);

                var activeSpecialSeason =
                    Assert.Single(unit.PricingSeasons, season =>
                            season.IsActive &&
                            season.Priority == 20);

                Assert.Equal(
                    activeSpecialSeason.NightlyRate.Amount,
                    quotedRoom.Quote.AccommodationPrice);

                Assert.Equal(
                    0m,
                    quotedRoom.Quote.ExtraGuestPrice);

                Assert.Equal(
                    activeSpecialSeason.NightlyRate.Amount,
                    quotedRoom.Quote.TotalPrice);

                Assert.Equal(
                    "USD",
                    quotedRoom.Quote.Currency);
            }

            // ==========================================
            // F. No se alteraron Payments del seed 23.3
            // ==========================================
            Assert.Equal(
                2,
                await dbContext.Payments.CountAsync(
                    cancellationToken));

            Assert.Equal(
                2,
                await dbContext.PaymentAttempts.CountAsync(
                    cancellationToken));

            // Los cuatro escenarios nuevos
            // pertenecen a la misma Property.
            foreach (DomainBooking booking in new[]
            {
                roomConflict,
                entireConflict,
                cancelled,
                seasonal
            })
            {
                Assert.Equal(propertyId, booking.PropertyId);

                Assert.Null(booking.CustomerSubjectId);

                Assert.NotNull(booking.GuestDetails);

                Assert.NotNull(booking.GuestAccessTokenHash);

                Assert.Matches(
                    "^[0-9A-F]{64}$",
                    booking.GuestAccessTokenHash);

                Assert.EndsWith(
                    "@bookify.invalid",
                    booking.GuestDetails.Email, StringComparison.Ordinal);
            }
        }

        // ==========================================
        // ACT — Segunda ejecución del seed
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

            DemoPaymentConsistencyValidator validator =
                retryScope.ServiceProvider
                    .GetRequiredService<
                        DemoPaymentConsistencyValidator>();

            await validator.ValidateAsync(
                cancellationToken);
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
        }
    }

    private static DomainBooking FindBooking(
        IEnumerable<DomainBooking> bookings,
        string email)
    {
        return Assert.Single(bookings, booking =>
                booking.GuestDetails?.Email == email);
    }
}
