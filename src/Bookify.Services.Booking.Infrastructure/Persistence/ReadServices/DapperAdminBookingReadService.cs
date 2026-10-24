using Bookify.Services.Booking.Application.Abstractions.Persistence;
using Bookify.Services.Booking.Application.Bookings.Administrative;
using Bookify.Services.Booking.Application.Bookings.Administrative.ReadModels;
using Bookify.Services.Booking.Application.Common.Pagination;
using Dapper;
using System.Data.Common;

namespace Bookify.Services.Booking.Infrastructure.Persistence.ReadServices;

internal sealed class DapperAdminBookingReadService : IAdminBookingReadService
{
    private const string ListSelectSql =
        """
        SELECT
            b.id AS "Id",
            b.booking_reference AS "BookingReference",
            b.property_id AS "PropertyId",
            b.rentable_unit_id AS "RentableUnitId",
            ru.name AS "RentableUnitName",
            b.customer_subject_id AS "CustomerSubjectId",
            bgd.full_name AS "GuestFullName",
            b.check_in_date AS "CheckInDate",
            b.check_out_date AS "CheckOutDate",
            b.guest_count AS "GuestCount",
            b.status AS "Status",
            bps.total_price_amount AS "TotalPrice",
            bps.total_price_currency AS "Currency",
            b.created_at_utc AS "CreatedAtUtc"
        FROM bookings AS b
        INNER JOIN rentable_units AS ru ON ru.id = b.rentable_unit_id
        LEFT JOIN booking_guest_details AS bgd ON bgd.booking_id = b.id
        LEFT JOIN booking_price_snapshots AS bps ON bps.booking_id = b.id
        """;

    private readonly IDbConnectionFactory _connectionFactory;

    public DapperAdminBookingReadService(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory ??
            throw new ArgumentNullException(nameof(connectionFactory));
    }

    public async Task<PagedResult<AdminBookingListItemReadModel>> GetPagedAsync(
        Guid propertyId,
        int pageNumber,
        int pageSize,
        string? status,
        DateOnly? rangeStart,
        DateOnly? rangeEnd,
        Guid? unitId, string? customerSubjectId,
        CancellationToken cancellationToken = default)
    {
        long offset = ((long)pageNumber - 1) * pageSize;

        var parameters = new DynamicParameters();

        parameters.Add("PropertyId", propertyId);
        parameters.Add("PageSize", pageSize);
        parameters.Add("Offset", offset);

        var conditions = new List<string>
        {
            "b.property_id = @PropertyId"
        };

        if (status is not null)
        {
            conditions.Add("b.status = @Status");
            parameters.Add("Status", status);
        }

        if (unitId.HasValue)
        {
            conditions.Add("b.rentable_unit_id = @UnitId");
            parameters.Add("UnitId", unitId.Value);
        }

        if (rangeStart.HasValue && rangeEnd.HasValue)
        {
            conditions.Add("b.check_in_date < @RangeEnd");
            conditions.Add("b.check_out_date > @RangeStart");
            parameters.Add("RangeStart", rangeStart.Value);
            parameters.Add("RangeEnd", rangeEnd.Value);
        }

        if (customerSubjectId is not null)
        {
            conditions.Add("b.customer_subject_id = @CustomerSubjectId");
            parameters.Add("CustomerSubjectId", customerSubjectId);
        }

        string whereClause = "WHERE " + string.Join(Environment.NewLine + "AND ", conditions);

        string sql =
            $"""
            {ListSelectSql}
            {whereClause}
            ORDER BY
                b.created_at_utc DESC NULLS LAST, b.id DESC
                LIMIT @PageSize
                OFFSET @Offset;

            SELECT COUNT(*) FROM bookings AS b {whereClause}
            """;

        await using DbConnection connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);

        var command = new CommandDefinition(
            sql,
            parameters,
            cancellationToken: cancellationToken);

        using SqlMapper.GridReader gridReader = await connection.QueryMultipleAsync(command);

        IEnumerable<AdminBookingListItemReadModel> rows = await gridReader.ReadAsync<AdminBookingListItemReadModel>();

        long totalRecords = await gridReader.ReadSingleAsync<long>();

        return new PagedResult<AdminBookingListItemReadModel>(
            rows.ToArray(),
            pageNumber,
            pageSize,
            totalRecords);
    }
}
