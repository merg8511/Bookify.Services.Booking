using Bookify.Services.Booking.Application.Abstractions.Persistence;
using Bookify.Services.Booking.Application.RentableUnits;
using Bookify.Services.Booking.Application.RentableUnits.ReadModels;
using Dapper;
using System.Data.Common;

namespace Bookify.Services.Booking.Infrastructure.Persistence.ReadServices;

internal sealed class DapperRentableUnitReadService : IRentableUnitReadService
{
    private const string GetByPropertyIdSql =
        """
            SELECT
                ru.id AS "Id",
                ru.property_id AS "PropertyId",
                ru.name AS "Name",
                ru.type AS "Type",
                ru.maximum_capacity AS "MaximumCapacity",
                ru.is_active AS "IsActive",
                (ru.type = 'EntireProperty') AS "IsEntireProperty"
            FROM rentable_units AS ru
            WHERE ru.property_id = @PropertyId
            ORDER BY
                ru.name,
                ru.id;
        """;

    private readonly IDbConnectionFactory _connectionFactory;

    public DapperRentableUnitReadService(
        IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory
            ?? throw new ArgumentNullException(nameof(connectionFactory));
    }

    public async Task<IReadOnlyList<RentableUnitListItemReadModel>>
        GetByPropertyIdAsync(
            Guid propertyId,
            CancellationToken cancellationToken = default)
    {
        await using DbConnection connection =
            await _connectionFactory.OpenConnectionAsync(cancellationToken);

        var command = new CommandDefinition(
                        GetByPropertyIdSql,
                        new
                        {
                            PropertyId = propertyId
                        },
                        cancellationToken: cancellationToken);

        IEnumerable<RentableUnitListItemReadModel> rows =
            await connection.QueryAsync<RentableUnitListItemReadModel>(command);

        return rows.ToArray();
    }
}
