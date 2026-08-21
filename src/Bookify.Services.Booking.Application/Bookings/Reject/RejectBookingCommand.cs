using Bookify.Services.Booking.Application.Abstractions.Messaging;

namespace Bookify.Services.Booking.Application.Bookings.Reject;

public sealed record RejectBookingCommand(Guid BookingId) : ICommand;
