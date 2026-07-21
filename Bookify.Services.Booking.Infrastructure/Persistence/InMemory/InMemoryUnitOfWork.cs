using Bookify.Services.Booking.Application.Abstractions.Persistence;
using Bookify.Services.Booking.Domain.Properties;

namespace Bookify.Services.Booking.Infrastructure.Persistence.InMemory;

internal sealed class InMemoryUnitOfWork : IUnitOfWork
{
    private readonly InMemoryBookingStore _store;
    private readonly List<Property> _pendingProperties = [];

    public InMemoryUnitOfWork(InMemoryBookingStore store)
    {
        _store = store;
    }

    internal void Stage(Property property)
    {
        ArgumentNullException.ThrowIfNull(property);

        _pendingProperties.Add(property);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        _store.CommitProperties(
            _pendingProperties);

        _pendingProperties.Clear();

        return Task.CompletedTask;
    }
}
