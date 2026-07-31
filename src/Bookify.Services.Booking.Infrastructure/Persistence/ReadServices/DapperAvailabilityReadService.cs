using Bookify.Services.Booking.Application.Abstractions.Persistence;
using Bookify.Services.Booking.Application.Availability;
using Bookify.Services.Booking.Application.Availability.ReadModels;
using Dapper;
using Microsoft.EntityFrameworkCore;
using System.Data.Common;

namespace Bookify.Services.Booking.Infrastructure.Persistence.ReadServices;

internal sealed class DapperAvailabilityReadService :
    IAvailabilityReadService
{
    private const string GetOverlappingBookingsSql =
        """
        SELECT
            b.id AS "BookingId",
            b.property_id AS "PropertyId",
            b.rentable_unit_id AS "RentableUnitId",
            ru.type AS "RentableUnitType,
            (ru.type = 'EntireProperty') AS "IsEntireProperty",
            b.check_in_date AS "CheckInDate",
            b.check_out_date AS "CheckOutDate",
            b.status AS "Status"
        FROM bookings AS b
        INNER JOIN rentable_units AS ru
            ON ru.id = b.rentable_unit_id
        WHERE b.property_id = @PropertyId
            AND b.check_in_date < @RequestedCheckOutDate
            AND b.check_out_date > @RequestedCheckInDate
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
        GetOverlappingBookingsAsync(
            Guid propertyId,
            DateOnly requestedCheckInDate,
            DateOnly requestedCheckOutDate,
            CancellationToken cancellationToken = default)
    {
        await using DbConnection connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);

        var command =
            new CommandDefinition(
                GetOverlappingBookingsSql,
                new
                {
                    PropertyId = propertyId,
                    RequestedCheckInDate = requestedCheckInDate,
                    RequestedCheckOutDate = requestedCheckOutDate,
                },
                cancellationToken: cancellationToken);

        IEnumerable<OverlappingBookingReadModel> rows =
            await connection.QueryAsync<OverlappingBookingReadModel>(command);

        return rows.ToArray();
    }
}
