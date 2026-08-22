using Bookify.Services.Booking.Application.Abstractions.DomainEvents;
using Bookify.Services.Booking.Domain.Shared.DomainEvents;
using Microsoft.Extensions.DependencyInjection;

namespace Bookify.Services.Booking.Application.DomainEvents;

public sealed class DomainEventDispatcher
    : IDomainEventDispatcher
{
    private readonly IServiceProvider _serviceProvider;

    public DomainEventDispatcher(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider ??
            throw new ArgumentNullException(nameof(serviceProvider));
    }

    public async Task DispatchAsync(
        IReadOnlyCollection<IDomainEvent> domainEvents,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvents);

        foreach (IDomainEvent domainEvent in domainEvents)
        {
            cancellationToken.ThrowIfCancellationRequested();

            await DispatchAsync(
                (dynamic)domainEvent,
                cancellationToken);
        }
    }

    private async Task DispatchAsync<TDomainEvent>(
        TDomainEvent domainEvent,
        CancellationToken cancellationToken) where TDomainEvent : IDomainEvent
    {
        IEnumerable<
            IDomainEventHandler<TDomainEvent>>
            handlers =
                _serviceProvider.GetServices<
                    IDomainEventHandler<
                        TDomainEvent>>();

        foreach (IDomainEventHandler<TDomainEvent> handler in handlers)
        {
            cancellationToken.ThrowIfCancellationRequested();

            await handler.HandleAsync(domainEvent, cancellationToken);
        }
    }
}
