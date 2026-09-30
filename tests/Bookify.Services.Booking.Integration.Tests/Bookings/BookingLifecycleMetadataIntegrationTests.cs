using Bookify.Services.Booking.Application.Abstractions.Persistence;
using Bookify.Services.Booking.Application.Abstractions.Persistence.Repositories;
using Bookify.Services.Booking.Application.Bookings;
using Bookify.Services.Booking.Application.Bookings.ReadModels;
using Bookify.Services.Booking.Domain.Bookings.Pricing;
using Bookify.Services.Booking.Domain.Bookings.ValueObjects;
using Bookify.Services.Booking.Domain.Shared;
using Bookify.Services.Booking.Domain.Shared.ValueObjects;
using Bookify.Services.Booking.Integration.Tests.Infrastructure;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using System.Data.Common;
using DomainBooking = Bookify.Services.Booking.Domain.Bookings.Booking;

namespace Bookify.Services.Booking.Integration.Tests.Bookings;

[Collection(BookingApiTestFixture.Name)]
[Trait("Category", "Integration")]
public sealed class BookingLifecycleMetadataIntegrationTests
{
    private static readonly DateTimeOffset CreatedAtUtc = new(2026, 9, 21, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset ApprovedAtUtc = CreatedAtUtc.AddHours(1);
    private static readonly DateTimeOffset PaidAtUtc = CreatedAtUtc.AddHours(2);
    private static readonly DateTimeOffset CompletedAtUtc = CreatedAtUtc.AddHours(3);
    private static readonly DateTimeOffset ApprovalDueAtUtc = CreatedAtUtc.AddHours(24);
    private static readonly DateTimeOffset PaymentDueAtUtc = ApprovedAtUtc.AddMinutes(30);

    private readonly BookingApiFactory _factory;

    public BookingLifecycleMetadataIntegrationTests(BookingApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task BookingMetadata_ShouldPersistWithEfAndProjectWithDapper()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        SeedData data = await BookingDatabaseTestSeeder.SeedPropertyWithRoomAsync(
            _factory.Services,
            cancellationToken: cancellationToken);

        StayPeriod stayPeriod = StayPeriod.Create(
            new DateOnly(2026, 11, 10),
            new DateOnly(2026, 11, 12)).Value;

        GuestCount guestCount = GuestCount.Create(2).Value;
        GuestDetails guestDetails = GuestDetails.Create("John Doe", "john@example.com", "+50377778888").Value;
        PriceSnapshot priceSnapshot = BookingTestData.CreatePriceSnapshot();

        Result<DomainBooking> creationResult = DomainBooking.Create(
            data.RentableUnit,
            stayPeriod,
            guestCount,
            guestDetails,
            priceSnapshot,
            CreatedAtUtc,
            ApprovalDueAtUtc);

        Assert.True(creationResult.IsSuccess);
        DomainBooking booking = creationResult.Value;
        string expectedReference = booking.Reference.Value;

        Result approvalResult = booking.Approve(ApprovedAtUtc, PaymentDueAtUtc);
        Assert.True(approvalResult.IsSuccess);

        Result paymentResult = booking.MarkAsPaid(PaidAtUtc);
        Assert.True(paymentResult.IsSuccess);

        Result completionResult = booking.Complete(CompletedAtUtc);
        Assert.True(completionResult.IsSuccess);

        // ACT - SAVE WITH EF
        using (IServiceScope saveScope = _factory.Services.CreateScope())
        {
            IBookingRepository repository = saveScope.ServiceProvider.GetRequiredService<IBookingRepository>();
            IUnitOfWork unitOfWork = saveScope.ServiceProvider.GetRequiredService<IUnitOfWork>();

            repository.Add(booking);
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        // ASSERT - EF CORE
        using (IServiceScope efScope = _factory.Services.CreateScope())
        {
            IBookingRepository repository = efScope.ServiceProvider.GetRequiredService<IBookingRepository>();
            DomainBooking? persisted = await repository.GetByIdAsync(booking.Id, cancellationToken);

            Assert.NotNull(persisted);
            Assert.Equal(expectedReference, persisted.Reference.Value);
            Assert.Equal(CreatedAtUtc, persisted.CreatedAtUtc);
            Assert.Equal(ApprovalDueAtUtc, persisted.ApprovalDueAtUtc);
            Assert.Equal(ApprovedAtUtc, persisted.ApprovedAtUtc);
            Assert.Equal(PaymentDueAtUtc, persisted.PaymentDueAtUtc);
            Assert.Equal(PaidAtUtc, persisted.PaidAtUtc);
            Assert.Equal(CompletedAtUtc, persisted.CompletedAtUtc);
            Assert.Null(persisted.CancelledAtUtc);
            Assert.Equal(priceSnapshot, persisted.PriceSnapshot);
        }

        // ASSERT - DAPPER
        using IServiceScope dapperScope = _factory.Services.CreateScope();
        IBookingReadService readService = dapperScope.ServiceProvider.GetRequiredService<IBookingReadService>();

        BookingDetailsReadModel? readModel = await readService.GetByIdAsync(booking.Id, cancellationToken);

        Assert.NotNull(readModel);
        Assert.Equal(expectedReference, readModel.BookingReference);
        Assert.Equal(CreatedAtUtc, readModel.CreatedAtUtc);
        Assert.Equal(ApprovalDueAtUtc, readModel.ApprovalDueAtUtc);
        Assert.Equal(ApprovedAtUtc, readModel.ApprovedAtUtc);
        Assert.Equal(PaymentDueAtUtc, readModel.PaymentDueAtUtc);
        Assert.Equal(PaidAtUtc, readModel.PaidAtUtc);
        Assert.Equal(CompletedAtUtc, readModel.CompletedAtUtc);
        Assert.Null(readModel.CancelledAtUtc);
        Assert.Equal(priceSnapshot.TotalPrice.Amount, readModel.TotalPrice);
        Assert.Equal(priceSnapshot.TotalPrice.Currency, readModel.Currency);
    }

    [Fact]
    public async Task BookingReference_ShouldBeUniqueInDatabase()
    {
        // ARRANGE
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        SeedData data = await BookingDatabaseTestSeeder.SeedPropertyWithRoomAsync(
            _factory.Services,
            cancellationToken: cancellationToken);

        Guid firstBookingId = Guid.NewGuid();
        Guid secondBookingId = Guid.NewGuid();
        string duplicatedReference = BookingTestReference.From(firstBookingId);

        IDbConnectionFactory connectionFactory = _factory.Services.GetRequiredService<IDbConnectionFactory>();
        await using DbConnection connection = await connectionFactory.OpenConnectionAsync(cancellationToken);

        var firstInsert = new CommandDefinition(
            """
            INSERT INTO bookings
            (
                id,
                booking_reference,
                property_id,
                rentable_unit_id,
                check_in_date,
                check_out_date,
                guest_count,
                status,
                cancellation_reason,
                guest_access_token_hash
            )
            VALUES
            (
                @BookingId,
                @BookingReference,
                @PropertyId,
                @RentableUnitId,
                @CheckInDate,
                @CheckOutDate,
                2,
                'PendingApproval',
                NULL,
                repeat('A', 64)
            );
            """,
            new
            {
                BookingId = firstBookingId,
                BookingReference = duplicatedReference,
                PropertyId = data.RentableUnit.PropertyId,
                RentableUnitId = data.RentableUnit.Id,
                CheckInDate = new DateOnly(2026, 12, 1),
                CheckOutDate = new DateOnly(2026, 12, 3)
            },
            cancellationToken: cancellationToken);

        await connection.ExecuteAsync(firstInsert);

        var duplicatedInsert = new CommandDefinition(
            """
            INSERT INTO bookings
            (
                id,
                booking_reference,
                property_id,
                rentable_unit_id,
                check_in_date,
                check_out_date,
                guest_count,
                status,
                cancellation_reason,
                guest_access_token_hash
            )
            VALUES
            (
                @BookingId,
                @BookingReference,
                @PropertyId,
                @RentableUnitId,
                @CheckInDate,
                @CheckOutDate,
                2,
                'PendingApproval',
                NULL,
                repeat('A', 64)
            );
            """,
            new
            {
                BookingId = secondBookingId,
                BookingReference = duplicatedReference,
                PropertyId = data.RentableUnit.PropertyId,
                RentableUnitId = data.RentableUnit.Id,
                CheckInDate = new DateOnly(2026, 12, 5),
                CheckOutDate = new DateOnly(2026, 12, 7)
            },
            cancellationToken: cancellationToken);

        // ACT
        async Task Action() => await connection.ExecuteAsync(duplicatedInsert);

        // ASSERT
        PostgresException exception = await Assert.ThrowsAsync<PostgresException>(Action);
        Assert.Equal(PostgresErrorCodes.UniqueViolation, exception.SqlState);
        Assert.Equal("ux_bookings_booking_reference", exception.ConstraintName);
    }
}
