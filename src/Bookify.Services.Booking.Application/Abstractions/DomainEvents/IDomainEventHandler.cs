using Bookify.Services.Booking.Domain.Shared.DomainEvents;

namespace Bookify.Services.Booking.Application.Abstractions.DomainEvents;

public interface IDomainEventHandler<in TDomainEvent>
    where TDomainEvent : IDomainEvent
{
    Task HandleAsync(
        TDomainEvent domainEvent,
        CancellationToken cancellationToken = default);
}
