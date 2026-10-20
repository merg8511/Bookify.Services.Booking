using Bookify.Services.Booking.Application.Abstractions.Persistence;
using Bookify.Services.Booking.Application.RentableUnits;
using Bookify.Services.Booking.Application.RentableUnits.ReadModels;
using Dapper;
using System.Data.Common;

namespace Bookify.Services.Booking.Infrastructure.Persistence.ReadServices;

internal sealed class DapperRentableUnitReadService : IRentableUnitReadService
{
    private const string GetActiveByPropertyIdSql =
        """
        SELECT
            ru.id AS "Id",
            ru.name AS "Name",
            ru.type AS "Type",
            ru.maximum_capacity AS "MaximumCapacity",
            ru.max_base_guests AS "MaxBaseGuests",
            (
                ru.type = 'EntireProperty'
            ) AS "IsEntireProperty"
        FROM rentable_units AS ru
        WHERE ru.property_id = @PropertyId
            AND ru.is_active = TRUE
        ORDER BY
            ru.name,
            ru.id;
        """;

    private const string AdminSelectSql =
        """
        SELECT
            ru.id AS "Id",
            ru.property_id AS "PropertyId",
            p.name AS "PropertyName",
            ru.name AS "Name",
            ru.type AS "Type",
            ru.maximum_capacity AS "MaximumCapacity",
            ru.max_base_guests AS "MaxBaseGuests",
            ru.is_active AS "IsActive",
            (
                ru.type = 'EntireProperty'
            ) AS "IsEntireProperty"
        FROM rentable_units AS ru
        INNER JOIN properties AS p ON p.id = ru.property_id
        """;

    private const string GetAllByPropertyIdSql =
        AdminSelectSql +
        "\n" +
        """
        WHERE ru.property_id = @PropertyId
        ORDER BY ru.name, ru.id;
        """;

    private const string GetAdminByIdSql =
        AdminSelectSql +
        "\n" +
        """
        WHERE ru.property_id = @PropertyId AND ru.id = @UnitId;
        """;

    private const string HasAnyBookingsSql =
        """
        SELECT EXISTS
        (
            SELECT 1
            FROM bookings AS b
            WHERE b.rentable_unit_id = @UnitId
        )
        """;

    private const string GetMaximumBookedGuestCountSql =
        """
        SELECT COALESCE(MAX(b.guest_count), 0)
        FROM bookings AS b
        WHERE b.rentable_unit_id = @UnitId;
        """;

    private readonly IDbConnectionFactory _connectionFactory;

    public DapperRentableUnitReadService(
        IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory
            ?? throw new ArgumentNullException(nameof(connectionFactory));
    }

    public async Task<IReadOnlyList<RentableUnitListItemReadModel>> GetActiveByPropertyIdAsync(
        Guid propertyId,
        CancellationToken cancellationToken = default)
    {
        await using DbConnection connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);

        var command = new CommandDefinition(
            GetActiveByPropertyIdSql,
            new
            {
                PropertyId = propertyId
            },
            cancellationToken: cancellationToken);

        IEnumerable<RentableUnitListItemReadModel> rows = await connection.
            QueryAsync<RentableUnitListItemReadModel>(command);

        return rows.ToArray();
    }

    public async Task<IReadOnlyList<RentableUnitDetailsReadModel>> GetAllByPropertyIdAsync(
        Guid propertyId,
        CancellationToken cancellationToken = default)
    {
        await using DbConnection connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);

        var command = new CommandDefinition(
            GetAllByPropertyIdSql,
            new { PropertyId = propertyId },
            cancellationToken: cancellationToken);

        IEnumerable<RentableUnitDetailsReadModel> rows = await connection
            .QueryAsync<RentableUnitDetailsReadModel>(command);

        return rows.ToArray();
    }

    public async Task<RentableUnitDetailsReadModel?> GetAdminByIdAsync(
        Guid propertyId,
        Guid unitId,
        CancellationToken cancellationToken = default)
    {
        await using DbConnection connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);

        var command = new CommandDefinition(
            GetAdminByIdSql,
            new
            {
                PropertyId = propertyId,
                UnitId = unitId
            },
            cancellationToken: cancellationToken);

        return await connection.QuerySingleOrDefaultAsync<RentableUnitDetailsReadModel>(command);
    }

    public async Task<bool> HasAnyBookingsAsync(
        Guid unitId,
        CancellationToken cancellationToken = default)
    {
        await using DbConnection connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);

        var command = new CommandDefinition(
            HasAnyBookingsSql,
            new { UnitId = unitId },
            cancellationToken: cancellationToken);

        return await connection.QuerySingleAsync<bool>(command);
    }

    public async Task<int> GetMaximumBookedGuestCountAsync(
        Guid unitId,
        CancellationToken cancellationToken = default)
    {
        await using DbConnection connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);

        var command = new CommandDefinition(
            GetMaximumBookedGuestCountSql,
            new { UnitId = unitId },
            cancellationToken: cancellationToken);

        return await connection.QuerySingleAsync<int>(command);
    }


}
