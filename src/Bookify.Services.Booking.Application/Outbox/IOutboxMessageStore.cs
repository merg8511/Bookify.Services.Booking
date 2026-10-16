namespace Bookify.Services.Booking.Application.Outbox;

public interface IOutboxMessageStore
{
    Task MarkProcessedAsync(
        Guid messageId,
        DateTimeOffset processedAtUtc,
        CancellationToken cancellationToken = default);

    Task<OutboxFailureOutcome> RecordFailureAsync(
        Guid messageId,
        DateTimeOffset failedAtUtc,
        string failureCode,
        int maxAttempts,
        TimeSpan baseDelay,
        TimeSpan maxDelay,
        bool isPermanent,
        CancellationToken cancellationToken = default);

    Task DeferAsync(
        Guid messageId,
        DateTimeOffset nextAttemptAtUtc,
        CancellationToken cancellationToken = default);
}
