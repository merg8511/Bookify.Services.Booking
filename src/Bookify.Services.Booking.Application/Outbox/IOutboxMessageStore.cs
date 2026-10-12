namespace Bookify.Services.Booking.Application.Outbox;

public interface IOutboxMessageStore
{
    Task MarkProcessedAsync(
        Guid messageId,
        DateTimeOffset processedAtUtc,
        CancellationToken cancellationToken = default);
}
