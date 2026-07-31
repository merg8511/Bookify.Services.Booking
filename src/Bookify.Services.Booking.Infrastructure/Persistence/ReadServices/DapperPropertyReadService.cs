using Bookify.Services.Booking.Application.Abstractions.Persistence;
using Bookify.Services.Booking.Application.Common.Pagination;
using Bookify.Services.Booking.Application.Common.Sorting;
using Bookify.Services.Booking.Application.Properties;
using Bookify.Services.Booking.Application.Properties.GetPaged;
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

    private const string NameFilterCondition =
        """
        p.name ILIKE @NamePattern
            ESCAPE E'\\'
        """;

    private const string ActiveFilterCondition =
        """
        p.is_active = @IsActive
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
        string? name,
        bool? isActive,
        PropertySortField sortField,
        SortDirection sortDirection,
        CancellationToken cancellationToken = default)
    {
        long offset = ((long)pageNumber - 1) *
            pageSize;

        var parameters = new DynamicParameters();

        parameters.Add(
            "PageSize",
            pageSize);

        parameters.Add(
            "Offset",
            offset);

        string whereClause =
            BuildWhereClause(
                name,
                isActive,
                parameters);

        string orderByClause =
            BuildOrderByClause(
                sortField,
                sortDirection);

        string sql =
            BuildPagedSql(whereClause, orderByClause);

        await using DbConnection connection =
            await _connectionFactory
                .OpenConnectionAsync(cancellationToken);

        var command = new CommandDefinition(
            sql,
            parameters,
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

    private static string BuildWhereClause(
        string? name,
        bool? isActive,
        DynamicParameters parameters)
    {
        var conditions = new List<string>();

        if (name is not null)
        {
            conditions.Add(NameFilterCondition);

            parameters.Add(
                "NamePattern",
                BuildContainsPattern(name));
        }

        if (isActive.HasValue)
        {
            conditions.Add(ActiveFilterCondition);

            parameters.Add(
                "IsActive",
                isActive.Value);
        }

        if (conditions.Count == 0)
        {
            return string.Empty;
        }

        return
            "WHERE " +
            string.Join(
                $"{Environment.NewLine}" +
                " AND ",
                conditions);
    }

    private static string BuildOrderByClause(
        PropertySortField sortField,
        SortDirection sortDirection)
    {
        return (sortField, sortDirection) switch
        {
            (PropertySortField.Name,
            SortDirection.Ascending) =>
                "p.name ASC, p.id ASC",

            (PropertySortField.Name,
            SortDirection.Descending) =>
                "p.name DESC, p.id ASC",

            (PropertySortField.IsActive,
            SortDirection.Ascending) =>
                "p.is_active ASC, " +
                "p.name ASC, " +
                "p.id ASC",

            (PropertySortField.IsActive,
            SortDirection.Descending) =>
                "p.is_active DESC, " +
                "p.name ASC, " +
                "p.id ASC",

            _ =>
                throw new ArgumentOutOfRangeException(
                    nameof(sortField),
                    sortField,
                    "The property sort combination " +
                    "is not supported.")
        };
    }

    private static string BuildPagedSql(string whereClause, string orderByClause)
    {
        return
            $"""
            SELECT
                p.id AS "Id",
                p.name AS "Name",
                p.is_active AS "IsActive"
            FROM properties AS p
            {whereClause}
            ORDER BY
                {orderByClause}
            LIMIT @PageSize
            OFFSET @Offset;

            SELECT
                COUNT(*)
            FROM properties AS p
            {whereClause};
            """;
    }

    private static string BuildContainsPattern(string value)
    {
        string escapedValue =
            value
                .Replace("\\", "\\\\", StringComparison.Ordinal)
                .Replace("%", "\\%", StringComparison.Ordinal)
                .Replace("_", "\\_", StringComparison.Ordinal);

        return $"%{escapedValue}%";
    }
}
