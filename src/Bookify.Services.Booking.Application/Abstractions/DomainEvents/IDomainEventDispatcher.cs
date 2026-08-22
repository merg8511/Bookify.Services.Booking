using Bookify.Services.Booking.Domain.Shared.DomainEvents;

namespace Bookify.Services.Booking.Application.Abstractions.DomainEvents;

public interface IDomainEventDispatcher
{
    Task DispatchAsync(
        IReadOnlyCollection<IDomainEvent> domainEvents,
        CancellationToken cancellationToken = default);
}
