using Bookify.Services.Booking.Application.Abstractions.Persistence;
using Bookify.Services.Booking.Application.Availability;
using Bookify.Services.Booking.Application.Availability.ReadModels;
using Bookify.Services.Booking.IntegrationTests.Infrastructure;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using System.Data.Common;

namespace Bookify.Services.Booking.IntegrationTests.ReadServices;

[Collection(BookingApiTestFixture.Name)]
[Trait("Category", "Integration")]
public sealed class DapperAvailabilityReadServiceTests
{
    private readonly BookingApiFactory _factory;

    public DapperAvailabilityReadServiceTests(
        BookingApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetOverlappingBookingsAsync_ReturnsOnlyTemporalIntersections()
    {
        // ARRANGE
        CancellationToken cancellationToken =
            TestContext.Current
                .CancellationToken;

        OverlapTestData data =
            await SeedAsync(
                cancellationToken);

        using IServiceScope scope =
            _factory.Services.CreateScope();

        IAvailabilityReadService readService =
            scope.ServiceProvider
                .GetRequiredService<
                    IAvailabilityReadService>();

        DateOnly requestedCheckInDate =
            Date(10);

        DateOnly requestedCheckOutDate =
            Date(15);

        // ACT
        IReadOnlyList<
            OverlappingBookingReadModel> result =
            await readService
                .GetInventoryConflictsAsync(
                    data.PropertyId,
                    data.RentableUnitId,
                    requestedCheckInDate,
                    requestedCheckOutDate,
                    cancellationToken);

        // ASSERT
        HashSet<Guid> returnedBookingIds =
            result
                .Select(
                    booking =>
                        booking.BookingId)
                .ToHashSet();

        Assert.Equal(data.ExpectedBookingIds, returnedBookingIds);

        Assert.DoesNotContain(
            data.EndsAtRequestedCheckInBookingId,
            returnedBookingIds);

        Assert.DoesNotContain(
            data.StartsAtRequestedCheckOutBookingId,
            returnedBookingIds);

        Assert.DoesNotContain(
            data.OtherPropertyBookingId,
            returnedBookingIds);

        Assert.All(
            result,
            booking =>
                Assert.Equal(
                    data.PropertyId,
                    booking.PropertyId));

        Assert.DoesNotContain(
            data.CancelledBookingId,
            returnedBookingIds);
    }

    [Fact]
    public async Task
    GetAvailableUnitsAsync_AppliesCompleteAvailabilityPolicy()
    {
        // Arrange
        CancellationToken cancellationToken =
            TestContext.Current
                .CancellationToken;

        AvailabilityTestData data =
            await SeedAvailabilityScenarioAsync(
                cancellationToken);

        using IServiceScope scope =
            _factory.Services.CreateScope();

        IAvailabilityReadService service =
            scope.ServiceProvider
                .GetRequiredService<
                    IAvailabilityReadService>();

        // Act
        IReadOnlyList<
            AvailableRentableUnitReadModel> result =
            await service.GetAvailableUnitsAsync(
                data.PropertyId,
                Date(10),
                Date(15),
                guestCount: 2,
                cancellationToken);

        // Assert
        AvailableRentableUnitReadModel availableUnit =
            Assert.Single(
                result);

        Assert.Equal(
            data.RoomBId,
            availableUnit.Id);

        Assert.Equal(
            "Room B",
            availableUnit.Name);

        Assert.Equal(
            2,
            availableUnit.MaximumCapacity);

        Assert.False(
            availableUnit.IsEntireProperty);

        Assert.DoesNotContain(
            result,
            unit =>
                unit.Id == data.RoomAId);

        Assert.DoesNotContain(
            result,
            unit =>
                unit.Id == data.EntirePropertyId);

        Assert.DoesNotContain(
            result,
            unit =>
                unit.Id == data.InactiveRoomId);

        Assert.DoesNotContain(
            result,
            unit =>
                unit.Id == data.LowCapacityRoomId);
    }

    private async Task<OverlapTestData> SeedAsync(
        CancellationToken cancellationToken)
    {
        Guid propertyId = Guid.NewGuid();
        Guid otherPropertyId = Guid.NewGuid();
        Guid rentableUnitId = Guid.NewGuid();
        Guid otherRentableUnitId = Guid.NewGuid();
        Guid identicalBookingId = Guid.NewGuid();
        Guid overlapsFromLeftBookingId = Guid.NewGuid();
        Guid overlapsFromRightBookingId = Guid.NewGuid();
        Guid containsRequestedBookingId = Guid.NewGuid();
        Guid cancelledBookingId = Guid.NewGuid();
        Guid endsAtRequestedCheckInBookingId = Guid.NewGuid();
        Guid startsAtRequestedCheckOutBookingId = Guid.NewGuid();
        Guid otherPropertyBookingId = Guid.NewGuid();

        IDbConnectionFactory connectionFactory =
            _factory.Services
                .GetRequiredService<
                    IDbConnectionFactory>();

        await using DbConnection connection =
            await connectionFactory
                .OpenConnectionAsync(
                    cancellationToken);

        await InsertPropertiesAsync(
            connection,
            propertyId,
            otherPropertyId,
            cancellationToken);

        await InsertRentableUnitsAsync(
            connection,
            propertyId,
            otherPropertyId,
            rentableUnitId,
            otherRentableUnitId,
            cancellationToken);

        BookingSeed[] bookings =
        [
            new(
                identicalBookingId,
                propertyId,
                rentableUnitId,
                Date(10),
                Date(15),
                "PendingApproval",
                null),

            new(
                overlapsFromLeftBookingId,
                propertyId,
                rentableUnitId,
                Date(8),
                Date(12),
                "PendingPayment",
                null),

            new(
                overlapsFromRightBookingId,
                propertyId,
                rentableUnitId,
                Date(14),
                Date(18),
                "Paid",
                null),

            new(
                containsRequestedBookingId,
                propertyId,
                rentableUnitId,
                Date(8),
                Date(20),
                "Paid",
                null),

            new(
                cancelledBookingId,
                propertyId,
                rentableUnitId,
                Date(11),
                Date(14),
                "Cancelled",
                "PaymentExpired"),

            new(
                endsAtRequestedCheckInBookingId,
                propertyId,
                rentableUnitId,
                Date(5),
                Date(10),
                "Paid",
                null),

            new(
                startsAtRequestedCheckOutBookingId,
                propertyId,
                rentableUnitId,
                Date(15),
                Date(20),
                "Paid",
                null),

            new(
                otherPropertyBookingId,
                otherPropertyId,
                otherRentableUnitId,
                Date(11),
                Date(14),
                "Paid",
                null)
        ];

        foreach (BookingSeed booking in bookings)
        {
            await InsertBookingAsync(
                connection,
                booking,
                cancellationToken);
        }

        return new OverlapTestData(
            propertyId,
            rentableUnitId,
            new HashSet<Guid>
            {
                identicalBookingId,
                overlapsFromLeftBookingId,
                overlapsFromRightBookingId,
                containsRequestedBookingId
            },
            cancelledBookingId,
            endsAtRequestedCheckInBookingId,
            startsAtRequestedCheckOutBookingId,
            otherPropertyBookingId);
    }

    private static async Task InsertPropertiesAsync(
        DbConnection connection,
        Guid propertyId,
        Guid otherPropertyId,
        CancellationToken cancellationToken)
    {
        var command =
            new CommandDefinition(
                """
                INSERT INTO properties
                (
                    id,
                    name,
                    time_zone_id,
                    check_in_time,
                    check_out_time,
                    is_active
                )
                VALUES
                (
                    @PropertyId,
                    'Availability Property',
                    'America/El_Salvador',
                    '15:00',
                    '11:00',
                    TRUE
                ),
                (
                    @OtherPropertyId,
                    'Other Property',
                    'America/El_Salvador',
                    '15:00',
                    '11:00',
                    TRUE
                );
                """,
                new
                {
                    PropertyId =
                        propertyId,

                    OtherPropertyId =
                        otherPropertyId
                },
                cancellationToken:
                    cancellationToken);

        await connection.ExecuteAsync(
            command);
    }

    private static async Task InsertRentableUnitsAsync(
        DbConnection connection,
        Guid propertyId,
        Guid otherPropertyId,
        Guid rentableUnitId,
        Guid otherRentableUnitId,
        CancellationToken cancellationToken)
    {
        var command =
            new CommandDefinition(
                """
                INSERT INTO rentable_units
                (
                    id,
                    property_id,
                    name,
                    type,
                    maximum_capacity,
                    max_base_guests,
                    is_active
                )
                VALUES
                (
                    @RentableUnitId,
                    @PropertyId,
                    'Main Room',
                    'Room',
                    4,
                    2,
                    TRUE
                ),
                (
                    @OtherRentableUnitId,
                    @OtherPropertyId,
                    'Other Room',
                    'Room',
                    4,
                    2,
                    TRUE
                );
                """,
                new
                {
                    RentableUnitId =
                        rentableUnitId,

                    PropertyId =
                        propertyId,

                    OtherRentableUnitId =
                        otherRentableUnitId,

                    OtherPropertyId =
                        otherPropertyId
                },
                cancellationToken:
                    cancellationToken);

        await connection.ExecuteAsync(
            command);
    }

    private static async Task InsertBookingAsync(
        DbConnection connection,
        BookingSeed booking,
        CancellationToken cancellationToken)
    {
        var command =
            new CommandDefinition(
                """
                INSERT INTO bookings
                (
                    id,
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
                    @Id,
                    @PropertyId,
                    @RentableUnitId,
                    @CheckInDate,
                    @CheckOutDate,
                    2,
                    @Status,
                    @CancellationReason
                );
                """,
                booking,
                cancellationToken:
                    cancellationToken);

        await connection.ExecuteAsync(
            command);
    }

    private static DateOnly Date(
        int day)
    {
        return new DateOnly(
            2026,
            8,
            day);
    }

    private sealed record BookingSeed(
        Guid Id,
        Guid PropertyId,
        Guid RentableUnitId,
        DateOnly CheckInDate,
        DateOnly CheckOutDate,
        string Status,
        string? CancellationReason);

    private sealed record OverlapTestData(
        Guid PropertyId,
        Guid RentableUnitId,
        IReadOnlySet<Guid> ExpectedBookingIds,
        Guid CancelledBookingId,
        Guid EndsAtRequestedCheckInBookingId,
        Guid StartsAtRequestedCheckOutBookingId,
        Guid OtherPropertyBookingId);

    private async Task<AvailabilityTestData> SeedAvailabilityScenarioAsync(
        CancellationToken cancellationToken)
    {
        Guid propertyId = Guid.NewGuid();
        Guid roomAId = Guid.NewGuid();
        Guid roomBId = Guid.NewGuid();
        Guid entirePropertyId = Guid.NewGuid();
        Guid inactiveRoomId = Guid.NewGuid();
        Guid lowCapacityRoomId = Guid.NewGuid();
        Guid roomABookingId = Guid.NewGuid();
        Guid roomBCancelledBookingId = Guid.NewGuid();

        IDbConnectionFactory connectionFactory =
            _factory.Services.GetRequiredService<IDbConnectionFactory>();

        await using DbConnection connection =
            await connectionFactory.OpenConnectionAsync(cancellationToken);

        var command = new CommandDefinition(
            """
            INSERT INTO properties
            (
                id,
                name,
                time_zone_id,
                check_in_time,
                check_out_time,
                is_active
            )
            VALUES
            (
                @PropertyId,
                'Availability Property',
                'America/El_Salvador',
                '15:00',
                '11:00',
                TRUE
            );

            INSERT INTO rentable_units
            (
                id,
                property_id,
                name,
                type,
                maximum_capacity,
                max_base_guests,
                is_active
            )
            VALUES
            (
                @RoomAId,
                @PropertyId,
                'Room A',
                'Room',
                4,
                2,
                TRUE
            ),
            (
                @RoomBId,
                @PropertyId,
                'Room B',
                'Room',
                2,
                2,
                TRUE
            ),
            (
                @EntirePropertyId,
                @PropertyId,
                'Entire Property',
                'EntireProperty',
                10,
                6,
                TRUE
            ),
            (
                @InactiveRoomId,
                @PropertyId,
                'Inactive Room',
                'Room',
                10,
                4,
                FALSE
            ),
            (
                @LowCapacityRoomId,
                @PropertyId,
                'Small Room',
                'Room',
                1,
                1,
                TRUE
            );

            INSERT INTO bookings
            (
                id,
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
                @RoomABookingId,
                @PropertyId,
                @RoomAId,
                @CheckInDate,
                @CheckOutDate,
                2,
                'Paid',
                NULL
            ),
            (
                @RoomBCancelledBookingId,
                @PropertyId,
                @RoomBId,
                @CheckInDate,
                @CheckOutDate,
                2,
                'Cancelled',
                'PaymentExpired'
            );
            """,
            new
            {
                PropertyId = propertyId,
                RoomAId = roomAId,
                RoomBId = roomBId,
                EntirePropertyId = entirePropertyId,
                InactiveRoomId = inactiveRoomId,
                LowCapacityRoomId = lowCapacityRoomId,
                RoomABookingId = roomABookingId,
                RoomBCancelledBookingId = roomBCancelledBookingId,
                CheckInDate = Date(10),
                CheckOutDate = Date(15)
            },
            cancellationToken: cancellationToken);

        await connection.ExecuteAsync(command);

        return new AvailabilityTestData(
            propertyId,
            roomAId,
            roomBId,
            entirePropertyId,
            inactiveRoomId,
            lowCapacityRoomId,
            roomABookingId,
            roomBCancelledBookingId);
    }

    private sealed record AvailabilityTestData(
        Guid PropertyId,
        Guid RoomAId,
        Guid RoomBId,
        Guid EntirePropertyId,
        Guid InactiveRoomId,
        Guid LowCapacityRoomId,
        Guid RoomABookingId,
        Guid RoomBCancelledBookingId);
}
