using Bookify.Services.Booking.Application.Abstractions.Persistence;
using Bookify.Services.Booking.Application.RentableUnits.Management.Seasons;
using Dapper;
using System.Data.Common;

namespace Bookify.Services.Booking.Infrastructure.Persistence.ReadServices;

internal sealed class DapperPricingSeasonReadService : IPricingSeasonReadService
{
    private const string SeasonSelectSql =
        """
        SELECT
            season.id AS "Id",
            season.rentable_unit_id AS "RentableUnitId",
            season.start_date AS "StartDate",
            season.end_date AS "EndDate",
            season.nightly_rate_amount AS "NightlyRate",
            season.nightly_rate_currency AS "Currency",
            season.priority AS "Priority",
            season.is_active AS "IsActive"
        FROM rentable_unit_pricing_seasons AS season
        """;

    private const string GetAllSql =
        SeasonSelectSql +
        "\n" +
        """
        WHERE season.rentable_unit_id = @UnitId
        ORDER BY season.start_date, season.priority DESC, season.id;
        """;

    private const string GetByIdSql =
        SeasonSelectSql +
        "\n" +
        """
        WHERE season.rentable_unit_id = @UnitId AND season.id = @SeasonId;
        """;


    private readonly IDbConnectionFactory _connectionFactory;

    public DapperPricingSeasonReadService(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory ??
            throw new ArgumentNullException(nameof(connectionFactory));
    }

    public async Task<IReadOnlyList<PricingSeasonReadModel>> GetAllAsync(
        Guid unitId,
        CancellationToken cancellationToken = default)
    {
        await using DbConnection connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);

        var command = new CommandDefinition(
            GetAllSql,
            new { UnitId = unitId },
            cancellationToken: cancellationToken);

        IEnumerable<PricingSeasonReadModel> rows = await connection.QueryAsync<PricingSeasonReadModel>(command);

        return rows.ToArray();
    }

    public async Task<PricingSeasonReadModel?> GetByIdAsync(
        Guid unitId,
        int seasonId,
        CancellationToken cancellationToken = default)
    {
        await using DbConnection connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);

        var command = new CommandDefinition(
            GetByIdSql,
            new
            {
                UnitId = unitId,
                SeasonId = seasonId
            },
            cancellationToken: cancellationToken);

        return await connection.QuerySingleOrDefaultAsync<PricingSeasonReadModel>(command);
    }
}
