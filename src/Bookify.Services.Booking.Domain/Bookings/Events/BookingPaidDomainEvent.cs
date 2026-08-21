using Bookify.Services.Booking.Domain.Shared.DomainEvents;

namespace Bookify.Services.Booking.Domain.Bookings.Events;

public sealed record BookingPaidDomainEvent(
    Guid BookingId)
    : IDomainEvent;
