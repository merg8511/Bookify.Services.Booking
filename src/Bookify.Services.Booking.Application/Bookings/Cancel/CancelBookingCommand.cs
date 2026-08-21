using Bookify.Services.Booking.Application.Abstractions.Messaging;

namespace Bookify.Services.Booking.Application.Bookings.Cancel;

public sealed record CancelBookingCommand(Guid BookingId)
    : ICommand;
