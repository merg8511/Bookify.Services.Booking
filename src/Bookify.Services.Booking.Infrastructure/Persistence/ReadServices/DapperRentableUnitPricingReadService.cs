using Bookify.Services.Booking.Application.Abstractions.Persistence;
using Bookify.Services.Booking.Application.RentableUnits.Management.Pricing;
using Dapper;
using System.Data.Common;

namespace Bookify.Services.Booking.Infrastructure.Persistence.ReadServices;

internal sealed class DapperRentableUnitPricingReadService : IRentableUnitPricingReadService
{
    private const string GetByUnitIdSql =
        """
        SELECT
            pricing.rentable_unit_id AS "RentableUnitId",
            pricing.regular_nightly_rate_amount AS "RegularNightlyRate",
            pricing.weekend_nightly_rate_amount AS "WeekendNightlyRate",
            pricing.extra_guest_nightly_rate_amount AS "ExtraGuestNightlyRate",
            pricing.regular_nightly_rate_currency AS "Currency"
        FROM rentable_unit_pricing AS pricing
        WHERE pricing.rentable_unit_id = @UnitId;
        """;

    private const string HasIncompatibleSeasonCurrencySql =
        """
        SELECT EXISTS
        (
            SELECT 1
            FROM rentable_unit_pricing_seasons AS season
            WHERE season.rentable_unit_id = @UnitId AND season.nightly_rate_currency <> @Currency
        );
        """;

    private readonly IDbConnectionFactory _connectionFactory;

    public DapperRentableUnitPricingReadService(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory ??
            throw new ArgumentNullException(nameof(connectionFactory));
    }

    public async Task<RentableUnitPricingReadModel?> GetByUnitIdAsync(
        Guid unitId,
        CancellationToken cancellationToken = default)
    {
        await using DbConnection connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);

        var command = new CommandDefinition(
            GetByUnitIdSql,
            new { UnitId = unitId },
            cancellationToken: cancellationToken);

        return await connection.QuerySingleOrDefaultAsync<RentableUnitPricingReadModel>(command);
    }

    public async Task<bool> HasIncompatibleSeasonCurrencyAsync(
        Guid unitId,
        string currency,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(currency);

        await using DbConnection connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);

        var command = new CommandDefinition(
            HasIncompatibleSeasonCurrencySql,
            new
            {
                UnitId = unitId,
                Currency = currency
            },
            cancellationToken: cancellationToken);

        return await connection.QuerySingleAsync<bool>(command);
    }
}
