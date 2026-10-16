using Bookify.Services.Booking.Application.Outbox;

namespace Bookify.Services.Booking.Infrastructure.Persistence.Outbox;

internal sealed class EfCoreOutboxMessageStore : IOutboxMessageStore
{
    private readonly BookingDbContext _dbContext;

    public EfCoreOutboxMessageStore(BookingDbContext dbContext)
    {
        _dbContext = dbContext ??
            throw new ArgumentNullException(nameof(dbContext));
    }

    public async Task MarkProcessedAsync(
        Guid messageId,
        DateTimeOffset processedAtUtc,
        CancellationToken cancellationToken = default)
    {
        OutboxMessage? message = await GetRequiredMessageAsync(messageId, cancellationToken);

        message.MarkProcessed(processedAtUtc);
    }

    public async Task<OutboxFailureOutcome> RecordFailureAsync(
        Guid messageId,
        DateTimeOffset failedAtUtc,
        string failureCode,
        int maxAttempts,
        TimeSpan baseDelay,
        TimeSpan maxDelay,
        bool isPermanent,
        CancellationToken cancellationToken = default)
    {
        OutboxMessage message = await GetRequiredMessageAsync(messageId, cancellationToken);

        return message.RecordFailure(
            failedAtUtc,
            failureCode,
            maxAttempts,
            baseDelay,
            maxDelay,
            isPermanent);
    }

    public async Task DeferAsync(
        Guid messageId,
        DateTimeOffset nextAttemptAtUtc,
        CancellationToken cancellationToken = default)
    {
        OutboxMessage message = await GetRequiredMessageAsync(messageId, cancellationToken);

        message.Defer(nextAttemptAtUtc);
    }

    private async Task<OutboxMessage> GetRequiredMessageAsync(
        Guid messageId,
        CancellationToken cancellationToken)
    {
        OutboxMessage? message = await _dbContext.OutboxMessages.FindAsync([messageId], cancellationToken);

        return message ??
            throw new InvalidOperationException(
                $"Outbox message '{messageId}' was not found.");
    }
}
