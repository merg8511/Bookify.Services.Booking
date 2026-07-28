using Bookify.Services.Booking.Application;
using Bookify.Services.Booking.Application.Abstractions.Persistence;
using Bookify.Services.Booking.Application.Bookings;
using Bookify.Services.Booking.Application.Bookings.ReadModels;
using Bookify.Services.Booking.Application.Properties;
using Bookify.Services.Booking.Application.Properties.ReadModels;
using Bookify.Services.Booking.Application.RentableUnits;
using Bookify.Services.Booking.Application.RentableUnits.ReadModels;
using Bookify.Services.Booking.IntegrationTests.Infrastructure;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using System.Data.Common;

namespace Bookify.Services.Booking.IntegrationTests.ReadServices;

[Collection(
    BookingApiTestFixture.Name)]
[Trait(
    "Category",
    "Integration")]
public sealed class DapperReadServicesTests
{
    private readonly BookingApiFactory _factory;

    public DapperReadServicesTests(
        BookingApiFactory factory)
    {
        _factory =
            factory;
    }

    [Fact]
    public async Task ReadServices_ReturnExpectedProjections()
    {
        CancellationToken cancellationToken =
            TestContext.Current
                .CancellationToken;

        TestData data =
            TestData.Create();

        await SeedAsync(
            data,
            cancellationToken);

        using IServiceScope scope =
            _factory.Services.CreateScope();

        IPropertyReadService propertyReadService =
            scope.ServiceProvider
                .GetRequiredService<
                    IPropertyReadService>();

        IRentableUnitReadService unitReadService =
            scope.ServiceProvider
                .GetRequiredService<
                    IRentableUnitReadService>();

        IBookingReadService bookingReadService =
            scope.ServiceProvider
                .GetRequiredService<
                    IBookingReadService>();

        PropertyDetailsReadModel? property =
            await propertyReadService.GetByIdAsync(
                data.PropertyId,
                cancellationToken);

        Assert.NotNull(
            property);

        Assert.Equal(
            data.PropertyId,
            property.Id);

        Assert.Equal(
            "Rancho Costa Azul",
            property.Name);

        IReadOnlyList<
            RentableUnitListItemReadModel> units =
            await unitReadService
                .GetByPropertyIdAsync(
                    data.PropertyId,
                    cancellationToken);

        Assert.Equal(
            2,
            units.Count);

        RentableUnitListItemReadModel
            entireProperty =
                Assert.Single(
                    units,
                    unit =>
                        unit.Id ==
                        data.EntirePropertyUnitId);

        Assert.True(
            entireProperty.IsEntireProperty);

        RentableUnitListItemReadModel room =
            Assert.Single(
                units,
                unit =>
                    unit.Id ==
                    data.RoomUnitId);

        Assert.False(
            room.IsEntireProperty);

        BookingDetailsReadModel? booking =
            await bookingReadService.GetByIdAsync(
                data.VisibleBookingId,
                cancellationToken);

        Assert.NotNull(
            booking);

        Assert.Equal(
            data.PropertyId,
            booking.PropertyId);

        Assert.Equal(
            data.RoomUnitId,
            booking.RentableUnitId);

        Assert.Equal(
            3,
            booking.NumberOfNights);

        Assert.Equal(
            2,
            booking.GuestCount);

        Assert.Equal(
            "PendingApproval",
            booking.Status);

        Assert.True(
            booking.BlocksInventory);

        IReadOnlyList<
            BookingCalendarItemReadModel> calendar =
            await bookingReadService
                .GetCalendarAsync(
                    data.PropertyId,
                    new DateOnly(
                        2026,
                        8,
                        10),
                    new DateOnly(
                        2026,
                        8,
                        20),
                    cancellationToken);

        BookingCalendarItemReadModel
            calendarBooking =
                Assert.Single(
                    calendar);

        Assert.Equal(
            data.VisibleBookingId,
            calendarBooking.BookingId);
    }

    private async Task SeedAsync(
        TestData data,
        CancellationToken cancellationToken)
    {
        IDbConnectionFactory connectionFactory =
            _factory.Services
                .GetRequiredService<
                    IDbConnectionFactory>();

        await using DbConnection connection =
            await connectionFactory
                .OpenConnectionAsync(
                    cancellationToken);

        var insertProperty =
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
                    @PropertyName,
                    @TimeZoneId,
                    @CheckInTime,
                    @CheckOutTime,
                    TRUE
                );
                """,
                new
                {
                    data.PropertyId,

                    PropertyName =
                        "Rancho Costa Azul",

                    TimeZoneId =
                        "America/El_Salvador",

                    CheckInTime =
                        new TimeOnly(
                            15,
                            0),

                    CheckOutTime =
                        new TimeOnly(
                            11,
                            0)
                },
                cancellationToken:
                    cancellationToken);

        await connection.ExecuteAsync(
            insertProperty);

        var insertUnits =
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
                    @EntirePropertyUnitId,
                    @PropertyId,
                    'Propiedad completa',
                    'EntireProperty',
                    12,
                    8,
                    TRUE
                ),
                (
                    @RoomUnitId,
                    @PropertyId,
                    'Habitación principal',
                    'Room',
                    4,
                    2,
                    TRUE
                );
                """,
                new
                {
                    data.EntirePropertyUnitId,
                    data.RoomUnitId,
                    data.PropertyId
                },
                cancellationToken:
                    cancellationToken);

        await connection.ExecuteAsync(
            insertUnits);

        var insertBookings =
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
                    @VisibleBookingId,
                    @PropertyId,
                    @RoomUnitId,
                    @VisibleCheckIn,
                    @VisibleCheckOut,
                    2,
                    'PendingApproval',
                    NULL
                ),
                (
                    @OutsideBookingId,
                    @PropertyId,
                    @RoomUnitId,
                    @OutsideCheckIn,
                    @OutsideCheckOut,
                    2,
                    'PendingApproval',
                    NULL
                );
                """,
                new
                {
                    data.VisibleBookingId,
                    data.OutsideBookingId,
                    data.PropertyId,
                    data.RoomUnitId,

                    VisibleCheckIn =
                        new DateOnly(
                            2026,
                            8,
                            12),

                    VisibleCheckOut =
                        new DateOnly(
                            2026,
                            8,
                            15),

                    OutsideCheckIn =
                        new DateOnly(
                            2026,
                            9,
                            1),

                    OutsideCheckOut =
                        new DateOnly(
                            2026,
                            9,
                            3)
                },
                cancellationToken:
                    cancellationToken);

        await connection.ExecuteAsync(
            insertBookings);
    }

    private sealed record TestData(
        Guid PropertyId,
        Guid EntirePropertyUnitId,
        Guid RoomUnitId,
        Guid VisibleBookingId,
        Guid OutsideBookingId)
    {
        public static TestData Create()
        {
            return new TestData(
                Guid.NewGuid(),
                Guid.NewGuid(),
                Guid.NewGuid(),
                Guid.NewGuid(),
                Guid.NewGuid());
        }
    }
}
