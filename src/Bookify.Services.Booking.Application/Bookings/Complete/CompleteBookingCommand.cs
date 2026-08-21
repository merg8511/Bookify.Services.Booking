using Bookify.Services.Booking.Application.Abstractions.Messaging;

namespace Bookify.Services.Booking.Application.Bookings.Complete;

public sealed record CompleteBookingCommand(Guid BookingId)
    : ICommand;

