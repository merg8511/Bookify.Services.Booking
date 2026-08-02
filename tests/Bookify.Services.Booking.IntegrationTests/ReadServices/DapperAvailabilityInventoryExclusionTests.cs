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
public sealed class DapperAvailabilityInventoryExclusionTests
{
    private readonly BookingApiFactory _factory;

    public DapperAvailabilityInventoryExclusionTests(BookingApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetInventoryConclictCandidatesAsync_AppliesInventoryExclusionMatrix()
    {
        // ARRANGE
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        TestData data = await SeedAsync(cancellationToken);

        using IServiceScope scope =
            _factory.Services.CreateScope();

        IAvailabilityReadService readService =
            scope.ServiceProvider
                .GetRequiredService<IAvailabilityReadService>();

        DateOnly requestedCheckInDate = Date(10);
        DateOnly requestedCheckOutDate = Date(15);

        // ACT
        IReadOnlyList<OverlappingBookingReadModel>
            roomAResult =
                await readService
                    .GetInventoryConflictCandidatesAsync(
                        data.PropertyId,
                        data.RoomAId,
                        requestedCheckInDate,
                        requestedCheckOutDate,
                        cancellationToken);

        IReadOnlyList<OverlappingBookingReadModel>
            roomBResult =
                await readService
                    .GetInventoryConflictCandidatesAsync(
                        data.PropertyId,
                        data.RoomBId,
                        requestedCheckInDate,
                        requestedCheckOutDate,
                        cancellationToken);

        IReadOnlyList<OverlappingBookingReadModel>
            entirePropertyResult =
                await readService
                    .GetInventoryConflictCandidatesAsync(
                        data.PropertyId,
                        data.EntirePropertyId,
                        requestedCheckInDate,
                        requestedCheckOutDate,
                        cancellationToken);

        // ASSERT
        AssertBookingIds(
            roomAResult,
            data.RoomABookingId,
            data.EntirePropertyBookingId);

        AssertBookingIds(
            roomBResult,
            data.RoomBBookingId,
            data.EntirePropertyBookingId);

        AssertBookingIds(
            entirePropertyResult,
            data.RoomABookingId,
            data.RoomBBookingId,
            data.EntirePropertyBookingId);

        Assert.DoesNotContain(
            entirePropertyResult,
            booking =>
                booking.BookingId ==
                data.AdjacentBookingId);

        Assert.DoesNotContain(
            entirePropertyResult,
            booking =>
                booking.BookingId ==
                data.OtherPropertyBookingId);

        OverlappingBookingReadModel
            cancelledEntirePropertyBooking =
                Assert.Single(
                    roomAResult,
                    booking =>
                        booking.BookingId ==
                        data.EntirePropertyBookingId);

        Assert.Equal(
            "Cancelled",
            cancelledEntirePropertyBooking.Status);

        Assert.True(
            cancelledEntirePropertyBooking
                .IsEntireProperty);
    }

    private async Task<TestData> SeedAsync(CancellationToken cancellationToken)
    {
        Guid propertyId = Guid.NewGuid();
        Guid otherPropertyId = Guid.NewGuid();
        Guid roomAId = Guid.NewGuid();
        Guid roomBId = Guid.NewGuid();
        Guid entirePropertyId = Guid.NewGuid();
        Guid otherPropertyRoomId = Guid.NewGuid();
        Guid roomABookingId = Guid.NewGuid();
        Guid roomBBookingId = Guid.NewGuid();
        Guid entirePropertyBookingId = Guid.NewGuid();
        Guid adjacentBookingId = Guid.NewGuid();
        Guid otherPropertyBookingId = Guid.NewGuid();

        IDbConnectionFactory connectionFactory = _factory.Services
            .GetRequiredService<IDbConnectionFactory>();

        await using DbConnection connection =
            await connectionFactory
                .OpenConnectionAsync(cancellationToken);

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
                    'Inventory Property',
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
                    4,
                    2,
                    TRUE
                ),
                (
                    @EntirePropertyId,
                    @PropertyId,
                    'Entire Property',
                    'EntireProperty',
                    20,
                    10,
                    TRUE
                ),
                (
                    @OtherPropertyRoomId,
                    @OtherPropertyId,
                    'Other Property Room',
                    'Room',
                    4,
                    2,
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
                    @RequestedCheckInDate,
                    @RequestedCheckOutDate,
                    2,
                    'PendingApproval',
                    NULL
                ),
                (
                    @RoomBBookingId,
                    @PropertyId,
                    @RoomBId,
                    @ContainedCheckInDate,
                    @ContainedCheckOutDate,
                    2,
                    'Paid',
                    NULL
                ),
                (
                    @EntirePropertyBookingId,
                    @PropertyId,
                    @EntirePropertyId,
                    @EntireCheckInDate,
                    @EntireCheckOutDate,
                    2,
                    'Cancelled',
                    'PaymentExpired'
                ),
                (
                    @AdjacentBookingId,
                    @PropertyId,
                    @RoomAId,
                    @AdjacentCheckInDate,
                    @RequestedCheckInDate,
                    2,
                    'Paid',
                    NULL
                ),
                (
                    @OtherPropertyBookingId,
                    @OtherPropertyId,
                    @OtherPropertyRoomId,
                    @ContainedCheckInDate,
                    @ContainedCheckOutDate,
                    2,
                    'Paid',
                    NULL
                );
                """,
                new
                {
                    PropertyId =
                        propertyId,

                    OtherPropertyId =
                        otherPropertyId,

                    RoomAId =
                        roomAId,

                    RoomBId =
                        roomBId,

                    EntirePropertyId =
                        entirePropertyId,

                    OtherPropertyRoomId =
                        otherPropertyRoomId,

                    RoomABookingId =
                        roomABookingId,

                    RoomBBookingId =
                        roomBBookingId,

                    EntirePropertyBookingId =
                        entirePropertyBookingId,

                    AdjacentBookingId =
                        adjacentBookingId,

                    OtherPropertyBookingId =
                        otherPropertyBookingId,

                    RequestedCheckInDate =
                        Date(10),

                    RequestedCheckOutDate =
                        Date(15),

                    ContainedCheckInDate =
                        Date(11),

                    ContainedCheckOutDate =
                        Date(14),

                    EntireCheckInDate =
                        Date(12),

                    EntireCheckOutDate =
                        Date(13),

                    AdjacentCheckInDate =
                        Date(5)
                },
                cancellationToken:
                    cancellationToken);

        await connection.ExecuteAsync(
            command);

        return new TestData(
            propertyId,
            roomAId,
            roomBId,
            entirePropertyId,
            roomABookingId,
            roomBBookingId,
            entirePropertyBookingId,
            adjacentBookingId,
            otherPropertyBookingId);
    }

    private static void AssertBookingIds(
        IReadOnlyList<
            OverlappingBookingReadModel> result,
        params Guid[] expectedBookingsIds)
    {
        HashSet<Guid> actualBookingIds =
            result.Select(
                booking => booking.BookingId)
            .ToHashSet();

        Assert.True(
            actualBookingIds.SetEquals(expectedBookingsIds));
    }

    private static DateOnly Date(int day)
    {
        return new DateOnly(
            2026, 08, day);
    }

    private sealed record TestData(
        Guid PropertyId,
        Guid RoomAId,
        Guid RoomBId,
        Guid EntirePropertyId,
        Guid RoomABookingId,
        Guid RoomBBookingId,
        Guid EntirePropertyBookingId,
        Guid AdjacentBookingId,
        Guid OtherPropertyBookingId);
}


