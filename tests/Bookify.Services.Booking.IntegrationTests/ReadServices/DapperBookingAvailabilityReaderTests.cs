using Bookify.Services.Booking.Application.Abstractions.Persistence;
using Bookify.Services.Booking.Application.Bookings.Create;
using Bookify.Services.Booking.IntegrationTests.Infrastructure;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using System.Data.Common;

namespace Bookify.Services.Booking.IntegrationTests.ReadServices;

[Collection(BookingApiTestFixture.Name)]
[Trait("Category", "Integration")]
public sealed class DapperBookingAvailabilityReaderTests
{
    private readonly BookingApiFactory _factory;

    public DapperBookingAvailabilityReaderTests(BookingApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task HasConflictAsync_AppliesCompleteInventoryConflictPolicy()
    {
        // ARRANGE
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        TestData data = await SeedAsync(cancellationToken);

        using IServiceScope scope = _factory.Services.CreateScope();

        IBookingAvailabilityReader reader =
            scope.ServiceProvider
                .GetRequiredService<
                    IBookingAvailabilityReader>();

        // ACT
        bool sameRoomConflict =
            await reader.HasConflictAsync(
                data.PropertyId,
                data.RoomAId,
                Date(10),
                Date(15),
                cancellationToken);

        bool differentRoomConflict =
            await reader.HasConflictAsync(
                data.PropertyId,
                data.RoomBId,
                Date(10),
                Date(15),
                cancellationToken);

        bool entirePropertyConflict =
            await reader.HasConflictAsync(
                data.PropertyId,
                data.EntirePropertyId,
                Date(10),
                Date(15),
                cancellationToken);

        bool adjacentPeriodConflict =
            await reader.HasConflictAsync(
                data.PropertyId,
                data.RoomAId,
                Date(15),
                Date(20),
                cancellationToken);

        bool roomBlockedByEntireProperty =
            await reader.HasConflictAsync(
                data.PropertyId,
                data.RoomBId,
                Date(20),
                Date(25),
                cancellationToken);

        // ASSERT
        Assert.True(sameRoomConflict);
        Assert.False(differentRoomConflict);
        Assert.True(entirePropertyConflict);
        Assert.False(adjacentPeriodConflict);
        Assert.True(roomBlockedByEntireProperty);
    }

    private async Task<TestData> SeedAsync(
        CancellationToken cancellationToken)
    {
        Guid propertyId = Guid.NewGuid();
        Guid roomAId = Guid.NewGuid();
        Guid roomBId = Guid.NewGuid();
        Guid entirePropertyId = Guid.NewGuid();
        Guid roomABookingId = Guid.NewGuid();
        Guid cancelledRoomBBookingId = Guid.NewGuid();
        Guid entirePropertyBookingId = Guid.NewGuid();

        IDbConnectionFactory connectionFactory =
            _factory.Services
                .GetRequiredService<
                    IDbConnectionFactory>();

        await using DbConnection connection = await connectionFactory.OpenConnectionAsync(cancellationToken);

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
                    'Booking Availability Property',
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
                    12,
                    8,
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
                    @FirstCheckInDate,
                    @FirstCheckOutDate,
                    2,
                    'Paid',
                    NULL
                ),
                (
                    @CancelledRoomBBookingId,
                    @PropertyId,
                    @RoomBId,
                    @FirstCheckInDate,
                    @FirstCheckOutDate,
                    2,
                    'Cancelled',
                    'PaymentExpired'
                ),
                (
                    @EntirePropertyBookingId,
                    @PropertyId,
                    @EntirePropertyId,
                    @SecondCheckInDate,
                    @SecondCheckOutDate,
                    2,
                    'PendingPayment',
                    NULL
                );
                """,
                new
                {
                    PropertyId = propertyId,
                    RoomAId = roomAId,
                    RoomBId = roomBId,
                    EntirePropertyId = entirePropertyId,
                    RoomABookingId = roomABookingId,
                    CancelledRoomBBookingId = cancelledRoomBBookingId,
                    EntirePropertyBookingId = entirePropertyBookingId,

                    FirstCheckInDate = Date(10),
                    FirstCheckOutDate = Date(15),
                    SecondCheckInDate = Date(20),
                    SecondCheckOutDate = Date(25)
                },
                cancellationToken:
                    cancellationToken);

        await connection.ExecuteAsync(command);

        return new TestData(
            propertyId,
            roomAId,
            roomBId,
            entirePropertyId);
    }

    private static DateOnly Date(
        int day)
    {
        return new DateOnly(
            2026,
            8,
            day);
    }

    private sealed record TestData(
        Guid PropertyId,
        Guid RoomAId,
        Guid RoomBId,
        Guid EntirePropertyId);
}
