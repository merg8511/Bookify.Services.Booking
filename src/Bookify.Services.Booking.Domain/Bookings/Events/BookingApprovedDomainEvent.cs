using Bookify.Services.Booking.Domain.Shared.DomainEvents;

namespace Bookify.Services.Booking.Domain.Bookings.Events;

public sealed record BookingApprovedDomainEvent(
    Guid BookingId)
    : IDomainEvent;
