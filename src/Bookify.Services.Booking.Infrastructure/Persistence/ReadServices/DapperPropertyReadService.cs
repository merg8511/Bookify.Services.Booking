using Bookify.Services.Booking.Application.Abstractions.Persistence;
using Bookify.Services.Booking.Application.Common.Pagination;
using Bookify.Services.Booking.Application.Properties;
using Bookify.Services.Booking.Application.Properties.ReadModels;
using Dapper;
using System.Data.Common;

namespace Bookify.Services.Booking.Infrastructure.Persistence.ReadServices;

internal sealed class DapperPropertyReadService
    : IPropertyReadService
{
    private const string GetByIdSql =
        """
        SELECT
            p.id AS "Id",
            p.name AS "Name",
            p.time_zone_id AS "TimeZoneId",
            p.check_in_time AS "CheckInTime",
            p.check_out_time AS "CheckOutTime",
            p.is_active AS "IsActive"
        FROM properties AS p
        WHERE p.id = @PropertyId;
        """;

    private const string GetPagedSql =
        """
        SELECT
            p.id AS "Id",
            p.name AS "Name",
            p.is_active AS "IsActive"
        FROM properties AS p
        ORDER BY
            p.name ASC,
            p.id ASC
        LIMIT @PageSize
        OFFSET @Offset;

        SELECT
            COUNT(*)
        FROM properties;
        """;
    private readonly IDbConnectionFactory _connectionFactory;

    public DapperPropertyReadService(
        IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory ??
            throw new ArgumentNullException(nameof(connectionFactory));
    }

    public async Task<PropertyDetailsReadModel?> GetByIdAsync(
        Guid propertyId,
        CancellationToken cancellationToken = default)
    {
        await using DbConnection connection =
            await _connectionFactory.OpenConnectionAsync(cancellationToken);

        var command = new CommandDefinition(
            GetByIdSql,
            new
            {
                PropertyId = propertyId
            },
            cancellationToken: cancellationToken);

        return await connection.QuerySingleOrDefaultAsync<PropertyDetailsReadModel>(command);
    }

    public async Task<PagedResult<PropertyListItemReadModel>> GetPagedAsync(
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        long offset = ((long)pageNumber - 1) *
            pageSize;

        await using DbConnection connection =
            await _connectionFactory
                .OpenConnectionAsync(cancellationToken);

        var command = new CommandDefinition(
            GetPagedSql,
            new
            {
                PageSize = pageSize,
                Offset = offset
            },
            cancellationToken: cancellationToken);

        using SqlMapper.GridReader gridReader =
            await connection.QueryMultipleAsync(command);

        IEnumerable<PropertyListItemReadModel> rows =
            await gridReader.ReadAsync<
                PropertyListItemReadModel>();

        long totalRecords =
            await gridReader.ReadSingleAsync<long>();

        return new PagedResult<
            PropertyListItemReadModel>(
            rows.ToArray(),
            pageNumber,
            pageSize,
            totalRecords);
    }
}
