using Bookify.Services.Booking.Application;
using Bookify.Services.Booking.Application.Abstractions.Persistence;
using Bookify.Services.Booking.Application.Abstractions.Persistence.Repositories;
using Bookify.Services.Booking.Application.Bookings;
using Bookify.Services.Booking.Application.Bookings.ReadModels;
using Bookify.Services.Booking.Domain.Bookings;
using Bookify.Services.Booking.Domain.Bookings.ValueObjects;
using Bookify.Services.Booking.Domain.Properties;
using Bookify.Services.Booking.Domain.Shared;
using Bookify.Services.Booking.Domain.Shared.ValueObjects;
using Bookify.Services.Booking.IntegrationTests.Infrastructure;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using System.Data.Common;

using DomainBooking =
    Bookify.Services.Booking.Domain.Bookings.Booking;

namespace Bookify.Services.Booking.IntegrationTests.Bookings;

[Collection(
    BookingApiTestFixture.Name)]
[Trait(
    "Category",
    "Integration")]
public sealed class BookingLifecycleMetadataIntegrationTests
{
    private static readonly DateTimeOffset CreatedAtUtc =
        new(
            2026,
            9,
            21,
            12,
            0,
            0,
            TimeSpan.Zero);

    private static readonly DateTimeOffset ApprovedAtUtc =
        CreatedAtUtc.AddHours(1);

    private static readonly DateTimeOffset PaidAtUtc =
        CreatedAtUtc.AddHours(2);

    private static readonly DateTimeOffset CompletedAtUtc =
        CreatedAtUtc.AddHours(3);

    private readonly BookingApiFactory _factory;

    public BookingLifecycleMetadataIntegrationTests(
        BookingApiFactory factory)
    {
        _factory =
            factory;
    }

    [Fact]
    public async Task
        BookingMetadata_ShouldPersistWithEfAndProjectWithDapper()
    {
        // ARRANGE
        CancellationToken cancellationToken =
            TestContext.Current
                .CancellationToken;

        SeedData data =
            await SeedPropertyWithRoomAsync(
                cancellationToken);

        StayPeriod stayPeriod =
            StayPeriod.Create(
                    new DateOnly(
                        2026,
                        11,
                        10),
                    new DateOnly(
                        2026,
                        11,
                        12))
                .Value;

        GuestCount guestCount =
            GuestCount.Create(
                    2)
                .Value;

        GuestDetails guestDetails =
            GuestDetails.Create(
                    "John Doe",
                    "john@example.com",
                    "+50377778888")
                .Value;

        Result<DomainBooking> creationResult =
            DomainBooking.Create(
                data.RentableUnit,
                stayPeriod,
                guestCount,
                guestDetails,
                CreatedAtUtc);

        Assert.True(
            creationResult.IsSuccess);

        DomainBooking booking =
            creationResult.Value;

        string expectedReference =
            booking.Reference.Value;

        Result approvalResult =
            booking.Approve(
                ApprovedAtUtc);

        Assert.True(
            approvalResult.IsSuccess);

        Result paymentResult =
            booking.MarkAsPaid(
                PaidAtUtc);

        Assert.True(
            paymentResult.IsSuccess);

        Result completionResult =
            booking.Complete(
                CompletedAtUtc);

        Assert.True(
            completionResult.IsSuccess);

        // ACT - SAVE WITH EF
        using (
            IServiceScope saveScope =
                _factory.Services.CreateScope())
        {
            IBookingRepository repository =
                saveScope.ServiceProvider
                    .GetRequiredService<
                        IBookingRepository>();

            IUnitOfWork unitOfWork =
                saveScope.ServiceProvider
                    .GetRequiredService<
                        IUnitOfWork>();

            repository.Add(
                booking);

            await unitOfWork.SaveChangesAsync(
                cancellationToken);
        }

        // ASSERT - EF CORE
        using (
            IServiceScope efScope =
                _factory.Services.CreateScope())
        {
            IBookingRepository repository =
                efScope.ServiceProvider
                    .GetRequiredService<
                        IBookingRepository>();

            DomainBooking? persisted =
                await repository.GetByIdAsync(
                    booking.Id,
                    cancellationToken);

            Assert.NotNull(
                persisted);

            Assert.Equal(
                expectedReference,
                persisted.Reference.Value);

            Assert.Equal(
                CreatedAtUtc,
                persisted.CreatedAtUtc);

            Assert.Equal(
                ApprovedAtUtc,
                persisted.ApprovedAtUtc);

            Assert.Equal(
                PaidAtUtc,
                persisted.PaidAtUtc);

            Assert.Equal(
                CompletedAtUtc,
                persisted.CompletedAtUtc);

            Assert.Null(
                persisted.ApprovalDueAtUtc);

            Assert.Null(
                persisted.PaymentDueAtUtc);

            Assert.Null(
                persisted.CancelledAtUtc);
        }

        // ASSERT - DAPPER
        using IServiceScope dapperScope =
            _factory.Services.CreateScope();

        IBookingReadService readService =
            dapperScope.ServiceProvider
                .GetRequiredService<
                    IBookingReadService>();

        BookingDetailsReadModel? readModel =
            await readService.GetByIdAsync(
                booking.Id,
                cancellationToken);

        Assert.NotNull(
            readModel);

        Assert.Equal(
            expectedReference,
            readModel.BookingReference);

        Assert.Equal(
            CreatedAtUtc,
            readModel.CreatedAtUtc);

        Assert.Equal(
            ApprovedAtUtc,
            readModel.ApprovedAtUtc);

        Assert.Equal(
            PaidAtUtc,
            readModel.PaidAtUtc);

        Assert.Equal(
            CompletedAtUtc,
            readModel.CompletedAtUtc);

        Assert.Null(
            readModel.ApprovalDueAtUtc);

        Assert.Null(
            readModel.PaymentDueAtUtc);

        Assert.Null(
            readModel.CancelledAtUtc);
    }

    [Fact]
    public async Task
        BookingReference_ShouldBeUniqueInDatabase()
    {
        // ARRANGE
        CancellationToken cancellationToken =
            TestContext.Current
                .CancellationToken;

        SeedData data =
            await SeedPropertyWithRoomAsync(
                cancellationToken);

        Guid firstBookingId =
            Guid.NewGuid();

        Guid secondBookingId =
            Guid.NewGuid();

        string duplicatedReference =
            BookingTestReference.From(
                firstBookingId);

        IDbConnectionFactory connectionFactory =
            _factory.Services
                .GetRequiredService<
                    IDbConnectionFactory>();

        await using DbConnection connection =
            await connectionFactory
                .OpenConnectionAsync(
                    cancellationToken);

        var firstInsert =
            new CommandDefinition(
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
                    cancellation_reason
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
                    NULL
                );
                """,
                new
                {
                    BookingId =
                        firstBookingId,

                    BookingReference =
                        duplicatedReference,

                    PropertyId =
                        data.RentableUnit.PropertyId,

                    RentableUnitId =
                        data.RentableUnit.Id,

                    CheckInDate =
                        new DateOnly(
                            2026,
                            12,
                            1),

                    CheckOutDate =
                        new DateOnly(
                            2026,
                            12,
                            3)
                },
                cancellationToken:
                    cancellationToken);

        await connection.ExecuteAsync(
            firstInsert);

        var duplicatedInsert =
            new CommandDefinition(
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
                    cancellation_reason
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
                    NULL
                );
                """,
                new
                {
                    BookingId =
                        secondBookingId,

                    BookingReference =
                        duplicatedReference,

                    PropertyId =
                        data.RentableUnit.PropertyId,

                    RentableUnitId =
                        data.RentableUnit.Id,

                    CheckInDate =
                        new DateOnly(
                            2026,
                            12,
                            5),

                    CheckOutDate =
                        new DateOnly(
                            2026,
                            12,
                            7)
                },
                cancellationToken:
                    cancellationToken);

        // ACT
        async Task Action()
        {
            await connection.ExecuteAsync(
                duplicatedInsert);
        }

        // ASSERT
        PostgresException exception =
            await Assert.ThrowsAsync<
                PostgresException>(
                    Action);

        Assert.Equal(
            PostgresErrorCodes.UniqueViolation,
            exception.SqlState);

        Assert.Equal(
            "ux_bookings_booking_reference",
            exception.ConstraintName);
    }

    private async Task<SeedData>
        SeedPropertyWithRoomAsync(
            CancellationToken cancellationToken)
    {
        Property property =
            Property.Create(
                    $"Lifecycle Metadata Test " +
                    $"{Guid.NewGuid():N}",
                    "America/El_Salvador",
                    new TimeOnly(
                        15,
                        0),
                    new TimeOnly(
                        11,
                        0))
                .Value;

        RentableUnit rentableUnit =
            RentableUnit.Create(
                    property.Id,
                    $"Room {Guid.NewGuid():N}",
                    RentableUnitType.Room,
                    maximumCapacity: 4,
                    maxBaseGuests: 2)
                .Value;

        using IServiceScope scope =
            _factory.Services
                .CreateScope();

        IPropertyRepository propertyRepository =
            scope.ServiceProvider
                .GetRequiredService<
                    IPropertyRepository>();

        IRentableUnitRepository rentableUnitRepository =
            scope.ServiceProvider
                .GetRequiredService<
                    IRentableUnitRepository>();

        IUnitOfWork unitOfWork =
            scope.ServiceProvider
                .GetRequiredService<
                    IUnitOfWork>();

        propertyRepository.Add(
            property);

        rentableUnitRepository.Add(
            rentableUnit);

        await unitOfWork.SaveChangesAsync(
            cancellationToken);

        return new SeedData(
            property.Id,
            rentableUnit);
    }

    private sealed record SeedData(
        Guid PropertyId,
        RentableUnit RentableUnit);
}
