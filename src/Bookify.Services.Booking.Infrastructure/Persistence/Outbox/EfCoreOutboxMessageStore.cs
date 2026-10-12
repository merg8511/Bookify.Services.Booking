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
        OutboxMessage? message = await _dbContext.OutboxMessages
            .FindAsync([messageId], cancellationToken);

        if (message is null)
        {
            throw new InvalidOperationException($"Outbox message '{messageId}' was not found.");
        }

        message.MarkProcessed(processedAtUtc);
    }
}
