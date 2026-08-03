using Bookify.Services.Booking.Application.Abstractions.Persistence;
using Bookify.Services.Booking.Application.Availability;
using Bookify.Services.Booking.Application.Availability.ReadModels;
using Dapper;
using System.Data.Common;

namespace Bookify.Services.Booking.Infrastructure.Persistence.ReadServices;

internal sealed class DapperAvailabilityReadService :
    IAvailabilityReadService
{
    private const string GetInventoryConflictsSql =
        """
        WITH requested_unit AS
        (
            SELECT
                ru.id,
                ru.property_id,
                ru.type
            FROM rentable_units AS ru
            WHERE ru.id = @RequestedRentableUnitId
                AND ru.property_id = @PropertyId
        )
        SELECT
            b.id AS "BookingId",
            b.property_id AS "PropertyId",
            b.rentable_unit_id AS "RentableUnitId",
            existing_unit.type AS "RentableUnitType",
            (
                existing_unit.type = 'EntireProperty'
            ) AS "IsEntireProperty",
            b.check_in_date AS "CheckInDate",
            b.check_out_date AS "CheckOutDate",
            b.status AS "Status"
        FROM requested_unit
        INNER JOIN bookings AS b
            ON b.property_id = requested_unit.property_id
        INNER JOIN rentable_units AS existing_unit
            ON existing_unit.id = b.rentable_unit_id
            AND existing_unit.property_id = b.property_id
        WHERE b.status IN
            (
                'PendingApproval',
                'PendingPayment',
                'Paid',
                'Completed'
            )
            AND b.check_in_date < @RequestedCheckOutDate
            AND b.check_out_date > @RequestedCheckInDate
            AND
            (
                existing_unit.id = requested_unit.id
                OR existing_unit.type = 'EntireProperty'
                OR requested_unit.type = 'EntireProperty'
            )
        ORDER BY
            b.check_in_date,
            b.check_out_date,
            b.id;
        """;

    private readonly IDbConnectionFactory _connectionFactory;

    public DapperAvailabilityReadService(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory
            ?? throw new ArgumentNullException(nameof(connectionFactory));
    }


    public async Task<
        IReadOnlyList<
            OverlappingBookingReadModel>>
        GetInventoryConflictsAsync(
            Guid propertyId,
            Guid requestedRentableUnitId,
            DateOnly requestedCheckInDate,
            DateOnly requestedCheckOutDate,
            CancellationToken cancellationToken = default)
    {
        await using DbConnection connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);

        var command =
            new CommandDefinition(
                GetInventoryConflictsSql,
                new
                {
                    PropertyId = propertyId,
                    RequestedRentableUnitId = requestedRentableUnitId,
                    RequestedCheckInDate = requestedCheckInDate,
                    RequestedCheckOutDate = requestedCheckOutDate,
                },
                cancellationToken: cancellationToken);

        IEnumerable<OverlappingBookingReadModel> rows =
            await connection.QueryAsync<OverlappingBookingReadModel>(command);

        return rows.ToArray();
    }
}
