using Bookify.Services.Booking.Application.Abstractions.Persistence;
using Bookify.Services.Booking.Application.BackgroundJobs;
using Dapper;
using System.Data;
using System.Data.Common;

namespace Bookify.Services.Booking.Infrastructure.Persistence.ReadServices;

internal sealed class DapperPendingBookingWorkSelector : IPendingBookingWorkSelector
{
    private const string GetExpiredApprovalBookingIdsSql =
        """
        SELECT
            b.id
        FROM bookings AS b
        WHERE b.status = 'PendingApproval'
            AND b.approval_due_at_utc IS NOT NULL
            AND b.approval_due_at_utc <= @NowUtc
        ORDER BY
            b.approval_due_at_utc,
            b.id
        LIMIT @BatchSize;
        """;

    private const string GetExpiredPaymentBookingIdsSql =
        """
        SELECT
            b.id
        FROM bookings AS b
        WHERE b.status = 'PendingPayment'
            AND b.payment_due_at_utc IS NOT NULL
            AND b.payment_due_at_utc <= @NowUtc
        ORDER BY
            b.payment_due_at_utc,
            b.id
        LIMIT @BatchSize
        """;

    private const string GetDueCompletionBookingIdsSql =
        """
        SELECT
            b.id
        FROM bookings AS b
        INNER JOIN properties AS p ON p.id = b.property_id
        WHERE b.status = 'Paid'
            AND b.check_out_date <= ((@NowUtc AT TIME ZONE 'UTC')::date +1)
            AND ((b.check_out_date + p.check_out_time) AT TIME ZONE p.time_zone_id) <= @NowUtc
        ORDER BY
            b.check_out_date,
            b.id
        LIMIT @BatchSize;
        """;

    private const string GetPaymentDeadlineReminderCandidateIdsSql =
        """
        SELECT
            b.id
        FROM bookings AS b
        WHERE b.status = 'PendingPayment'
            AND b.payment_due_at_utc IS NOT NULL
            AND b.payment_due_at_utc >= @WindowStartUtc
            AND b.payment_due_at_utc < @WindowEndUtc
        ORDER BY
            b.payment_due_at_utc,
            b.id
        LIMIT @BatchSize;
        """;

    private const string GetUpcomingCheckInReminderCandidateIdsSql =
        """
        SELECT
            b.id
        FROM bookings AS b
        INNER JOIN properties AS p ON p.id = b.property_id
        WHERE b.status = 'Paid'
            AND b.check_in_date >= ((@WindowStartUtc AT TIME ZONE 'UTC')::date - 1)
            AND b.check_in_date <= ((@WindowEndUtc AT TIME ZONE 'UTC')::date + 1)
            AND ((b.check_in_date + p.check_in_time) AT TIME ZONE p.time_zone_id) >= @WindowStartUtc
            AND ((b.check_in_date + p.check_in_time) AT TIME ZONE p.time_zone_id) < @WindowEndUtc
        ORDER BY
            b.check_in_date,
            b.id
        LIMIT @BatchSize;
        """;

    private readonly IDbConnectionFactory _connectionFactory;

    public DapperPendingBookingWorkSelector(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory
            ?? throw new ArgumentNullException(nameof(connectionFactory));
    }

    public Task<IReadOnlyList<Guid>> GetExpiredApprovalBookingIdsAsync(
        DateTimeOffset nowUtc,
        int batchSize,
        CancellationToken cancellationToken = default)
    {
        EnsureValidBatchSize(batchSize);

        return QueryBookingIdsAsync(
            GetExpiredApprovalBookingIdsSql,
            new
            {
                NowUtc = nowUtc.ToUniversalTime(),
                BatchSize = batchSize
            },
            cancellationToken);
    }

    public Task<IReadOnlyList<Guid>> GetExpiredPaymentBookingIdsAsync(
        DateTimeOffset nowUtc,
        int batchSize,
        CancellationToken cancellationToken = default)
    {
        EnsureValidBatchSize(batchSize);

        return QueryBookingIdsAsync(
            GetExpiredPaymentBookingIdsSql,
            new
            {
                NowUtc = nowUtc.ToUniversalTime(),
                BatchSize = batchSize
            },
            cancellationToken);
    }

    public Task<IReadOnlyList<Guid>> GetDueCompletionBookingIdsAsync(
        DateTimeOffset nowUtc,
        int batchSize,
        CancellationToken cancellationToken = default)
    {
        EnsureValidBatchSize(batchSize);

        return QueryBookingIdsAsync(
            GetDueCompletionBookingIdsSql,
            new
            {
                NowUtc = nowUtc.ToUniversalTime(),
                BatchSize = batchSize
            },
            cancellationToken);
    }

    public Task<IReadOnlyList<Guid>> GetPaymentDeadlineReminderCandidateIdsAsync(
        DateTimeOffset windowStartUtc,
        DateTimeOffset windowEndUtc,
        int batchSize,
        CancellationToken cancellationToken = default)
    {
        EnsureValidWindow(windowStartUtc, windowEndUtc);
        EnsureValidBatchSize(batchSize);

        return QueryBookingIdsAsync(
            GetPaymentDeadlineReminderCandidateIdsSql,
            new
            {
                WindowStartUtc = windowStartUtc.ToUniversalTime(),
                WindowEndUtc = windowEndUtc.ToUniversalTime(),
                BatchSize = batchSize
            },
            cancellationToken);
    }

    public Task<IReadOnlyList<Guid>> GetUpcomingCheckInReminderCandidateIdsAsync(
        DateTimeOffset windowStartUtc,
        DateTimeOffset windowEndUtc,
        int batchSize, CancellationToken cancellationToken = default)
    {
        EnsureValidWindow(windowStartUtc, windowEndUtc);
        EnsureValidBatchSize(batchSize);

        return QueryBookingIdsAsync(
            GetUpcomingCheckInReminderCandidateIdsSql,
            new
            {
                WindowStartUtc = windowStartUtc.ToUniversalTime(),
                WindowEndUtc = windowEndUtc.ToUniversalTime(),
                BatchSize = batchSize
            },
            cancellationToken);
    }

    private static void EnsureValidBatchSize(int batchSize)
    {
        if (batchSize <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(batchSize),
                batchSize,
                "Batch size must be greater than zero.");
        }
    }

    private async Task<IReadOnlyList<Guid>> QueryBookingIdsAsync(
        string sql,
        object parameters,
        CancellationToken cancellationToken)
    {
        await using DbConnection connection = await _connectionFactory
            .OpenConnectionAsync(cancellationToken);

        var command = new CommandDefinition(
            sql,
            parameters,
            cancellationToken: cancellationToken);

        IEnumerable<Guid> bookingIds = await connection.QueryAsync<Guid>(command);

        return bookingIds.ToArray();
    }

    private static void EnsureValidWindow(
        DateTimeOffset windowStartUtc,
        DateTimeOffset windowEndUtc)
    {
        if (windowEndUtc <= windowStartUtc)
        {
            throw new ArgumentException(
                "The work selection window end must be greater than its start.",
                nameof(windowEndUtc));
        }
    }
}
