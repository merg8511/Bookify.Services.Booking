using Bookify.Services.Booking.Application.Abstractions.Persistence;
using Bookify.Services.Booking.Application.Payments.Administrative;
using Bookify.Services.Booking.Application.Payments.Administrative.ReadModels;
using Dapper;
using System.Data.Common;

namespace Bookify.Services.Booking.Infrastructure.Persistence.ReadServices;

internal sealed class DapperAdminPaymentReadService : IAdminPaymentReadService
{
    private const string GetByBookingSql =
    """
    SELECT
        b.id AS "BookingId",
        b.status AS "BookingStatus",

        p.id AS "PaymentId",
        p.status AS "PaymentStatus",
        p.amount AS "PaymentAmount",
        p.currency AS "PaymentCurrency",
        p.created_at_utc AS "PaymentCreatedAtUtc",
        p.updated_at_utc AS "PaymentUpdatedAtUtc",
        p.completed_at_utc AS "PaymentCompletedAtUtc",
        (p.pending_initiation_key IS NOT NULL) AS "HasPendingInitiation",
        p.pending_initiation_created_at_utc AS "PendingInitiationCreatedAtUtc",

        pa.id AS "AttemptId",
        pa.status AS "AttemptStatus",
        pa.external_reference AS "ExternalReference",
        pa.amount AS "AttemptAmount",
        pa.currency AS "AttemptCurrency",
        pa.created_at_utc AS "AttemptCreatedAtUtc",
        pa.completed_at_utc AS "AttemptCompletedAtUtc"
    FROM bookings AS b LEFT JOIN payments AS p ON p.booking_id = b.id
    LEFT JOIN payment_attempts AS pa ON pa.payment_id = p.id
    WHERE b.id = @BookingId AND b.property_id = @PropertyId
    ORDER BY
        pa.created_at_utc DESC NULLS LAST,
        pa.id DESC;
    """;

    private readonly IDbConnectionFactory _connectionFactory;

    public DapperAdminPaymentReadService(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory ??
            throw new ArgumentNullException(nameof(connectionFactory));
    }

    public async Task<AdminBookingPaymentReadModel?> GetByBookingAsync(
        Guid propertyId,
        Guid bookingId,
        CancellationToken cancellationToken = default)
    {
        await using DbConnection connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);

        var command = new CommandDefinition(
            GetByBookingSql,
            new
            {
                PropertyId = propertyId,
                BookingId = bookingId
            },
            cancellationToken: cancellationToken);

        IEnumerable<AdminPaymentSqlRow> queryRows = await connection
            .QueryAsync<AdminPaymentSqlRow>(command);

        AdminPaymentSqlRow[] rows = queryRows.ToArray();

        if (rows.Length == 0)
        {
            return null;
        }

        AdminPaymentSqlRow header = rows[0];

        if (!header.PaymentId.HasValue)
        {
            return new AdminBookingPaymentReadModel(
                header.BookingId,
                header.BookingStatus,
                Payment: null);
        }

        AdminPaymentAttemptReadModel[] attempts = rows
            .Where(row => row.AttemptId.HasValue)
            .Select(row => new AdminPaymentAttemptReadModel(
                row.AttemptId!.Value,
                row.AttemptStatus!,
                row.ExternalReference!,
                row.AttemptAmount!.Value,
                row.AttemptCurrency!,
                row.AttemptCreatedAtUtc!.Value,
                row.AttemptCompletedAtUtc))
            .ToArray();

        var payment = new AdminPaymentReadModel(
            header.PaymentId.Value,
            header.PaymentStatus!,
            header.PaymentAmount!.Value,
            header.PaymentCurrency!,
            header.PaymentCreatedAtUtc!.Value,
            header.PaymentUpdatedAtUtc!.Value,
            header.PaymentCompletedAtUtc,
            header.HasPendingInitiation,
            header.PendingInitiationCreatedAtUtc,
            attempts);

        return new AdminBookingPaymentReadModel(
            header.BookingId,
            header.BookingStatus,
            payment);
    }
}

internal sealed class AdminPaymentSqlRow
{
    public Guid BookingId { get; set; }
    public string BookingStatus { get; set; } = string.Empty;
    public Guid? PaymentId { get; set; }
    public string? PaymentStatus { get; set; }
    public decimal? PaymentAmount { get; set; }
    public string? PaymentCurrency { get; set; }
    public DateTimeOffset? PaymentCreatedAtUtc { get; set; }
    public DateTimeOffset? PaymentUpdatedAtUtc { get; set; }
    public DateTimeOffset? PaymentCompletedAtUtc { get; set; }
    public bool HasPendingInitiation { get; set; }
    public DateTimeOffset? PendingInitiationCreatedAtUtc { get; set; }
    public Guid? AttemptId { get; set; }
    public string? AttemptStatus { get; set; }
    public string? ExternalReference { get; set; }
    public decimal? AttemptAmount { get; set; }
    public string? AttemptCurrency { get; set; }
    public DateTimeOffset? AttemptCreatedAtUtc { get; set; }
    public DateTimeOffset? AttemptCompletedAtUtc { get; set; }

}
