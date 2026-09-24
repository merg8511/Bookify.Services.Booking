using Bookify.Services.Booking.Application;
using Bookify.Services.Booking.Application.Abstractions.Persistence;
using Bookify.Services.Booking.Application.Bookings;
using Bookify.Services.Booking.Application.Bookings.ReadModels;
using Dapper;
using System.Data.Common;

namespace Bookify.Services.Booking.Infrastructure.Persistence.ReadServices;

internal sealed class DapperBookingReadService : IBookingReadService
{
    private const string BookingDetailsSelectSql =
        """
        SELECT
            b.id AS "Id",
            b.booking_reference AS "BookingReference",

            b.property_id AS "PropertyId",
            p.name AS "PropertyName",

            b.rentable_unit_id AS "RentableUnitId",
            ru.name AS "RentableUnitName",

            b.check_in_date AS "CheckInDate",
            b.check_out_date AS "CheckOutDate",
            (
                b.check_out_date -
                b.check_in_date
            ) AS "NumberOfNights",

            b.guest_count AS "GuestCount",

            bgd.full_name AS "GuestFullName",
            bgd.email AS "GuestEmail",
            bgd.phone AS "GuestPhone",

            bps.accommodation_price_amount AS "AccommodationPrice",
            bps.extra_guest_price_amount AS "ExtraGuestPrice",
            bps.total_price_amount AS "TotalPrice",
            bps.total_price_currency AS "Currency",
            b.status AS "Status",
            b.cancellation_reason AS "CancellationReason",
            payment.status AS "PaymentStatus",
            b.created_at_utc AS "CreatedAtUtc",
            b.approval_due_at_utc AS "ApprovalDueAtUtc",
            b.approved_at_utc AS "ApprovedAtUtc",
            b.payment_due_at_utc AS "PaymentDueAtUtc",
            b.paid_at_utc AS "PaidAtUtc",
            b.cancelled_at_utc AS "CancelledAtUtc",
            b.completed_at_utc AS "CompletedAtUtc",
            (
                b.status IN
                (
                    'PendingApproval',
                    'PendingPayment',
                    'Paid',
                    'Completed'
                )
            ) AS "BlocksInventory"
        FROM bookings AS b
        INNER JOIN properties AS p ON p.id = b.property_id
        INNER JOIN rentable_units AS ru ON ru.id = b.rentable_unit_id
        LEFT JOIN booking_guest_details AS bgd ON bgd.booking_id = b.id
        LEFT JOIN booking_price_snapshots AS bps ON bps.booking_id = b.id
        LEFT JOIN payments AS payment ON payment.booking_id = b.id
        """;

    private const string GetByIdSql = BookingDetailsSelectSql +
        "\n" +
        """
        WHERE b.id = @BookingId;
        """;

    private const string GetByReferenceSql = BookingDetailsSelectSql +
        "\n" +
        """
        WHERE b.booking_reference = @BookingReference;
        """;

    private const string GetCalendarSql =
        """
        SELECT
            b.id AS "BookingId",
            b.rentable_unit_id AS "RentableUnitId",
            ru.name AS "RentableUnitName",
            b.check_in_date AS "CheckInDate",
            b.check_out_date AS "CheckOutDate",
            b.guest_count AS "GuestCount",
            b.status AS "Status",
            (
                b.status IN
                (
                    'PendingApproval',
                    'PendingPayment',
                    'Paid',
                    'Completed'
                )
            ) AS "BlocksInventory"
        FROM bookings AS b
        INNER JOIN rentable_units AS ru
            ON ru.id = b.rentable_unit_id
        WHERE b.property_id = @PropertyId
            AND b.check_in_date < @RangeEnd
            AND b.check_out_date > @RangeStart
        ORDER BY
            b.check_in_date,
            ru.name,
            b.id;
        """;

    private readonly IDbConnectionFactory _connectionFactory;

    public DapperBookingReadService(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory ??
            throw new ArgumentNullException(nameof(connectionFactory));
    }

    public async Task<BookingDetailsReadModel?> GetByIdAsync(
        Guid bookingId,
        CancellationToken cancellationToken = default)
    {
        await using DbConnection connection = await _connectionFactory
            .OpenConnectionAsync(cancellationToken);

        var command =
            new CommandDefinition(
                GetByIdSql,
                new
                {
                    BookingId = bookingId
                },
                cancellationToken: cancellationToken);

        return await connection.QuerySingleOrDefaultAsync<BookingDetailsReadModel>(command);
    }

    public async Task<BookingDetailsReadModel?> GetByReferenceAsync(
        string bookingReference,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bookingReference);

        await using DbConnection connection = await _connectionFactory
            .OpenConnectionAsync(cancellationToken);

        var command = new CommandDefinition(
            GetByReferenceSql,
            new
            {
                BookingReference = bookingReference
            },
            cancellationToken: cancellationToken);

        return await connection.QuerySingleOrDefaultAsync<BookingDetailsReadModel>(command);
    }

    public async Task<IReadOnlyList<BookingCalendarItemReadModel>> GetCalendarAsync(
            Guid propertyId,
            DateOnly rangeStart,
            DateOnly rangeEnd,
            CancellationToken cancellationToken = default)
    {
        await using DbConnection connection = await _connectionFactory
            .OpenConnectionAsync(cancellationToken);

        var command = new CommandDefinition(
                GetCalendarSql,
                new
                {
                    PropertyId = propertyId,
                    RangeStart = rangeStart,
                    RangeEnd = rangeEnd
                },
                cancellationToken: cancellationToken);

        IEnumerable<BookingCalendarItemReadModel> rows = await connection
                .QueryAsync<BookingCalendarItemReadModel>(command);

        return rows.ToArray();
    }
}
