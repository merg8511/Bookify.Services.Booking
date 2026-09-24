using Bookify.Services.Booking.Application.Abstractions.Persistence;
using Bookify.Services.Booking.Application.Availability;
using Bookify.Services.Booking.Application.Availability.ReadModels;
using Dapper;
using System.Data.Common;

namespace Bookify.Services.Booking.Infrastructure.Persistence.ReadServices;

internal sealed class DapperAvailabilityReadService :
    IAvailabilityReadService
{
    private const string GetInventoryConflictsSql =
        """
        WITH requested_unit AS
        (
            SELECT
                ru.id,
                ru.property_id,
                ru.type
            FROM rentable_units AS ru
            WHERE ru.id = @RequestedRentableUnitId
                AND ru.property_id = @PropertyId
        )
        SELECT
            b.id AS "BookingId",
            b.property_id AS "PropertyId",
            b.rentable_unit_id AS "RentableUnitId",
            existing_unit.type AS "RentableUnitType",
            (
                existing_unit.type = 'EntireProperty'
            ) AS "IsEntireProperty",
            b.check_in_date AS "CheckInDate",
            b.check_out_date AS "CheckOutDate",
            b.status AS "Status"
        FROM requested_unit
        INNER JOIN bookings AS b
            ON b.property_id = requested_unit.property_id
        INNER JOIN rentable_units AS existing_unit
            ON existing_unit.id = b.rentable_unit_id
            AND existing_unit.property_id = b.property_id
        WHERE b.status IN
            (
                'PendingApproval',
                'PendingPayment',
                'Paid',
                'Completed'
            )
            AND b.check_in_date < @RequestedCheckOutDate
            AND b.check_out_date > @RequestedCheckInDate
            AND
            (
                existing_unit.id = requested_unit.id
                OR existing_unit.type = 'EntireProperty'
                OR requested_unit.type = 'EntireProperty'
            )
        ORDER BY
            b.check_in_date,
            b.check_out_date,
            b.id;
        """;

    private const string GetAvailableUnitsSql =
        """
        SELECT
            requested_unit.id AS "Id",
            requested_unit.property_id AS "PropertyId",
            requested_unit.name AS "Name",
            requested_unit.type AS "Type",
            requested_unit.maximum_capacity AS "MaximumCapacity",
            requested_unit.max_base_guests AS "MaxBaseGuests",

            (
                requested_unit.type = 'EntireProperty'
            ) AS "IsEntireProperty",

            pricing.regular_nightly_rate_amount AS "RegularNightlyRateAmount",
            pricing.weekend_nightly_rate_amount AS "WeekendNightlyRateAmount",
            pricing.extra_guest_nightly_rate_amount AS "ExtraGuestNightlyRateAmount",
            pricing.regular_nightly_rate_currency AS "PricingCurrency"

        FROM rentable_units AS requested_unit
        INNER JOIN properties AS p ON p.id = requested_unit.property_id
        INNER JOIN rentable_unit_pricing AS pricing ON pricing.rentable_unit_id = requested_unit.id
        WHERE p.id = @PropertyId
            AND p.is_active = TRUE
            AND requested_unit.is_active = TRUE
            AND requested_unit.maximum_capacity >= @GuestCount
            AND NOT EXISTS
            (
                SELECT 1
                FROM bookings AS b
                INNER JOIN rentable_units AS existing_unit ON existing_unit.id = b.rentable_unit_id
                    AND existing_unit.property_id = b.property_id
                WHERE b.property_id = requested_unit.property_id
                    AND b.status IN
                    (
                        'PendingApproval',
                        'PendingPayment',
                        'Paid',
                        'Completed'
                    )
                    AND b.check_in_date < @RequestedCheckOutDate
                    AND b.check_out_date > @RequestedCheckInDate
                    AND
                    (
                        existing_unit.id = requested_unit.id
                        OR existing_unit.type = 'EntireProperty'
                        OR requested_unit.type = 'EntireProperty'
                    )
            )
        ORDER BY
            requested_unit.name,
            requested_unit.id;
        """;

    private const string GetPricingSeasonsSql =
        """
        SELECT
            season.rentable_unit_id AS "RentableUnitId",
            season.start_date AS "StartDate",
            season.end_date AS "EndDate",
            season.nightly_rate_amount AS "NightlyRateAmount",
            season.nightly_rate_currency AS "Currency",
            season.priority AS "Priority"
        FROM rentable_unit_pricing_seasons AS season
        WHERE season.rentable_unit_id = ANY(@RentableUnitIds)
            AND season.start_date < @RequestedCheckOutDate
            AND season.end_date > @RequestedCheckInDate

        ORDER BY
            season.rentable_unit_id,
            season.priority DESC,
            season.start_date,
            season.end_date
        """;

    private readonly IDbConnectionFactory _connectionFactory;

    public DapperAvailabilityReadService(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory
            ?? throw new ArgumentNullException(nameof(connectionFactory));
    }


    public async Task<IReadOnlyList<OverlappingBookingReadModel>> GetInventoryConflictsAsync(
        Guid propertyId,
        Guid requestedRentableUnitId,
        DateOnly requestedCheckInDate,
        DateOnly requestedCheckOutDate,
        CancellationToken cancellationToken = default)
    {
        await using DbConnection connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);

        var command = new CommandDefinition(
            GetInventoryConflictsSql,
            new
            {
                PropertyId = propertyId,
                RequestedRentableUnitId = requestedRentableUnitId,
                RequestedCheckInDate = requestedCheckInDate,
                RequestedCheckOutDate = requestedCheckOutDate,
            },
            cancellationToken: cancellationToken);

        IEnumerable<OverlappingBookingReadModel> rows = await connection.QueryAsync<OverlappingBookingReadModel>(command);

        return rows.ToArray();
    }

    public async Task<IReadOnlyList<AvailableRentableUnitReadModel>> GetAvailableUnitsAsync(
        Guid propertyId,
        DateOnly requestedCheckInDate,
        DateOnly requestedCheckOutDate,
        int guestCount,
        CancellationToken cancellationToken = default)
    {
        await using DbConnection connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);

        var command = new CommandDefinition(
            GetAvailableUnitsSql,
            new
            {
                PropertyId = propertyId,
                RequestedCheckInDate = requestedCheckInDate,
                RequestedCheckOutDate = requestedCheckOutDate,
                GuestCount = guestCount
            },
            cancellationToken: cancellationToken);

        IEnumerable<
            AvailableRentableUnitReadModel> rows =
            await connection.QueryAsync<
                AvailableRentableUnitReadModel>(command);

        return rows.ToArray();
    }

    public async Task<IReadOnlyList<AvailabilityPricingSeasonReadModel>> GetPricingSeasonsAsync(
        IReadOnlyCollection<Guid> rentableUnitIds,
        DateOnly requestedCheckInDate,
        DateOnly requestedCheckOutDate,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(rentableUnitIds);

        Guid[] ids = rentableUnitIds.Distinct().ToArray();

        if (ids.Length == 0)
        {
            return Array.Empty<AvailabilityPricingSeasonReadModel>();
        }

        await using DbConnection connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);

        var command = new CommandDefinition(
            GetPricingSeasonsSql,
            new
            {
                RentableUnitIds = ids,
                RequestedCheckInDate = requestedCheckInDate,
                RequestedCheckOutDate = requestedCheckOutDate
            },
            cancellationToken: cancellationToken);

        IEnumerable<AvailabilityPricingSeasonReadModel> rows = await connection
            .QueryAsync<AvailabilityPricingSeasonReadModel>(command);

        return rows.ToArray();
    }
}
