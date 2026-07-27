using Bookify.Services.Booking.Application.Abstractions.Persistence;
using Bookify.Services.Booking.Application.Properties.GetById;
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
}
