using Bookify.Services.Booking.Application.Abstractions.Persistence;
using Bookify.Services.Booking.Application.RentableUnits;
using Bookify.Services.Booking.Application.RentableUnits.ReadModels;
using Dapper;
using System.Data.Common;

namespace Bookify.Services.Booking.Infrastructure.Persistence.ReadServices;

internal sealed class DapperRentableUnitReadService : IRentableUnitReadService
{
    private const string RentableUnitProjectionSql =
        """
        SELECT
            ru.id AS "Id",
            ru.property_id AS "PropertyId",
            ru.name AS "Name",
            ru.type AS "Type",
            ru.maximum_capacity AS "MaximumCapacity",
            ru.max_base_guests AS "MaxBaseGuests",
            ru.is_active AS "IsActive",
            (
                ru.type = 'EntireProperty'
            ) AS "IsEntireProperty"
        FROM rentable_units AS ru
        """;

    private const string GetByPropertyIdSql =
        RentableUnitProjectionSql +
        "\n" +
        """
        WHERE ru.property_id = @PropertyId
        ORDER BY ru.name, ru.id;
        """;
    private const string GetActiveByPropertyIdSql =
        RentableUnitProjectionSql +
        "\n" +
        """
        WHERE ru.property_id =  @PropertyId
            AND ru.is_active = TRUE
        ORDER BY ru.name, ru.id;
        """;


    private readonly IDbConnectionFactory _connectionFactory;

    public DapperRentableUnitReadService(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory
            ?? throw new ArgumentNullException(nameof(connectionFactory));
    }



    public async Task<IReadOnlyList<RentableUnitListItemReadModel>> GetByPropertyIdAsync(
        Guid propertyId,
        CancellationToken cancellationToken = default)
    {
        return await QueryAsync(
            GetByPropertyIdSql,
            propertyId,
            cancellationToken);
    }

    public async Task<IReadOnlyList<RentableUnitListItemReadModel>> GetActiveByPropertyIdAsync(
        Guid propertyId,
        CancellationToken cancellationToken = default)
    {
        return await QueryAsync(
            GetActiveByPropertyIdSql,
            propertyId,
            cancellationToken);
    }

    private async Task<IReadOnlyList<RentableUnitListItemReadModel>> QueryAsync(
        string sql,
        Guid propertyId,
        CancellationToken cancellationToken)
    {
        await using DbConnection connection = await _connectionFactory
            .OpenConnectionAsync(cancellationToken);

        var command = new CommandDefinition(
            sql,
            new
            {
                PropertyId = propertyId
            },
            cancellationToken: cancellationToken);

        IEnumerable<RentableUnitListItemReadModel> rows = await connection
            .QueryAsync<RentableUnitListItemReadModel>(command);

        return rows.ToArray();
    }
}
