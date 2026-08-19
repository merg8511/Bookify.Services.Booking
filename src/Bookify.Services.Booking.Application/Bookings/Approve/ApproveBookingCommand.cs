using Bookify.Services.Booking.Application.Abstractions.Messaging;

namespace Bookify.Services.Booking.Application.Bookings.Approve;

public sealed record ApproveBookingCommand(Guid BookingId)
    : ICommand;
