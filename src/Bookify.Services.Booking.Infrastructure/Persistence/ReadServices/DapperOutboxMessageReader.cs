using Bookify.Services.Booking.Application.Abstractions.Persistence;
using Bookify.Services.Booking.Application.Outbox;
using Dapper;
using System.Data.Common;

namespace Bookify.Services.Booking.Infrastructure.Persistence.ReadServices;

internal sealed class DapperOutboxMessageReader : IOutboxMessageReader
{
    private const string GetPendingMessagesSql =
        """
        SELECT
            id AS "Id",
            event_type AS "EventType",
            payload::text AS "Payload",
            created_at_utc AS "CreatedAtUtc"
        FROM outbox_messages
        WHERE processed_at_utc IS NULL
            AND failed_at_utc IS NULL
            AND
            (
                next_attempt_at_utc IS NULL
                OR next_attempt_at_utc <= CURRENT_TIMESTAMP
            )
        ORDER BY
            created_at_utc,
            id
        LIMIT @BatchSize;
        """;

    private readonly IDbConnectionFactory _connectionFactory;

    public DapperOutboxMessageReader(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory ??
            throw new ArgumentNullException(nameof(connectionFactory));
    }


    public async Task<IReadOnlyList<PendingOutboxMessageReadModel>> GetPendingMessagesAsync(
        int batchSize,
        CancellationToken cancellationToken = default)
    {
        if (batchSize <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(batchSize),
                batchSize,
                "Batch size must be greater than zero.");
        }

        await using DbConnection connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);

        var command = new CommandDefinition(
            GetPendingMessagesSql,
            new
            {
                BatchSize = batchSize
            },
             cancellationToken: cancellationToken);

        IEnumerable<PendingOutboxMessageReadModel> messages = await connection
            .QueryAsync<PendingOutboxMessageReadModel>(command);

        return messages.ToArray();
    }
}
