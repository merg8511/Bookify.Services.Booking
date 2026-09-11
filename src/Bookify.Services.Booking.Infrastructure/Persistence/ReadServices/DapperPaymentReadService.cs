using Bookify.Services.Booking.Application.Abstractions.Persistence;
using Bookify.Services.Booking.Application.Payments;
using Bookify.Services.Booking.Application.Payments.ReadModels;
using Dapper;
using System.Data.Common;

namespace Bookify.Services.Booking.Infrastructure.Persistence.ReadServices;

internal sealed class DapperPaymentReadService : IPaymentReadService
{
    private const string GetStatusByBookingIdSql =
        """
        SELECT
            b.id AS "BookingId",
            b.status AS "BookingStatus",
            p.status AS "PaymentStatus",
            latest_attempt.payment_attempt_status AS "PaymentAttemptStatus"
        FROM bookings AS b
        LEFT JOIN payments AS p
            ON p.booking_id = b.id
        LEFT JOIN LATERAL
        (
            SELECT
                pa.status AS payment_attempt_status
            FROM payment_attempts AS pa
            WHERE pa.payment_id = p.id
            ORDER BY
                pa.created_at_utc DESC,
                pa.id DESC
            LIMIT 1
        ) AS latest_attempt
            ON TRUE
        WHERE b.id = @BookingId;
        """;

    private readonly IDbConnectionFactory _connectionFactory;

    public DapperPaymentReadService(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory ??
            throw new ArgumentNullException(nameof(connectionFactory));
    }

    public async Task<PaymentStatusReadModel?> GetStatusByBookingIdAsync(
        Guid bookingId,
        CancellationToken cancellationToken = default)
    {
        await using DbConnection connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);

        var command = new CommandDefinition(
            GetStatusByBookingIdSql,
            new
            {
                BookingId = bookingId
            },
            cancellationToken: cancellationToken);

        return await connection.QuerySingleOrDefaultAsync<PaymentStatusReadModel>(command);
    }
}
