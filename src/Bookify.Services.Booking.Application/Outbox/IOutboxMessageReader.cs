namespace Bookify.Services.Booking.Application.Outbox;

public interface IOutboxMessageReader
{
    Task<IReadOnlyList<PendingOutboxMessageReadModel>> GetPendingMessagesAsync(
        int batchSize,
        CancellationToken cancellationToken = default);
}
