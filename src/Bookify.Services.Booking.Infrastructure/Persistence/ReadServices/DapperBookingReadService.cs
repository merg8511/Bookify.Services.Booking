using Bookify.Services.Booking.Application;
using Bookify.Services.Booking.Application.Abstractions.Persistence;
using Bookify.Services.Booking.Application.Bookings;
using Bookify.Services.Booking.Application.Bookings.ReadModels;
using Dapper;
using System.Data.Common;

namespace Bookify.Services.Booking.Infrastructure.Persistence.ReadServices;

internal sealed class DapperBookingReadService : IBookingReadService
{
    private const string GetByIdSql =
        """
        SELECT
            b.id AS "Id",
            b.property_id AS "PropertyId",
            b.rentable_unit_id AS "RentableUnitId",
            ru.name AS "RentableUnitName",
            b.check_in_date AS "CheckInDate",
            b.check_out_date AS "CheckOutDate",
            (
                b.check_out_date -
                b.check_in_date
            ) AS "NumberOfNights",
            b.guest_count AS "GuestCount",
            b.status AS "Status",
            b.cancellation_reason AS "CancellationReason",
            (
                b.status <> 'Cancelled'
            ) AS "BlocksInventory"
        FROM bookings AS b
        INNER JOIN rentable_units AS ru
            ON ru.id = b.rentable_unit_id
        WHERE b.id = @BookingId;
        """;

    private const string GetCalendarSql =
        """
        SELECT
            b.id AS "BookingId",
            b.rentable_unit_id AS "RentableUnitId",
            ru.name AS "RentableUnitName",
            b.check_in_date AS "CheckInDate",
            b.check_out_date AS "CheckOutDate",
            b.guest_count AS "GuestCount",
            b.status AS "Status",
            (
                b.status <> 'Cancelled'
            ) AS "BlocksInventory"
        FROM bookings AS b
        INNER JOIN rentable_units AS ru
            ON ru.id = b.rentable_unit_id
        WHERE b.property_id = @PropertyId
            AND b.check_in_date < @RangeEnd
            AND b.check_out_date > @RangeStart
        ORDER BY
            b.check_in_date,
            ru.name,
            b.id;
        """;

    private readonly IDbConnectionFactory _connectionFactory;

    public DapperBookingReadService(
        IDbConnectionFactory connectionFactory)
    {
        _connectionFactory =
            connectionFactory ??
            throw new ArgumentNullException(
                nameof(connectionFactory));
    }

    public async Task<BookingDetailsReadModel?> GetByIdAsync(
        Guid bookingId,
        CancellationToken cancellationToken = default)
    {
        await using DbConnection connection =
            await _connectionFactory
                .OpenConnectionAsync(
                    cancellationToken);

        var command =
            new CommandDefinition(
                GetByIdSql,
                new
                {
                    BookingId = bookingId
                },
                cancellationToken:
                    cancellationToken);

        return await connection
            .QuerySingleOrDefaultAsync<
                BookingDetailsReadModel>(
                    command);
    }

    public async Task<
        IReadOnlyList<BookingCalendarItemReadModel>>
        GetCalendarAsync(
            Guid propertyId,
            DateOnly rangeStart,
            DateOnly rangeEnd,
            CancellationToken cancellationToken = default)
    {
        await using DbConnection connection =
            await _connectionFactory
                .OpenConnectionAsync(
                    cancellationToken);

        var command =
            new CommandDefinition(
                GetCalendarSql,
                new
                {
                    PropertyId =
                        propertyId,

                    RangeStart =
                        rangeStart,

                    RangeEnd =
                        rangeEnd
                },
                cancellationToken:
                    cancellationToken);

        IEnumerable<
            BookingCalendarItemReadModel> rows =
            await connection
                .QueryAsync<
                    BookingCalendarItemReadModel>(
                        command);

        return rows.ToArray();
    }
}
