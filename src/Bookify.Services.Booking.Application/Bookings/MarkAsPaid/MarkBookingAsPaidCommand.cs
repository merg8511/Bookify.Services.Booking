using Bookify.Services.Booking.Application.Abstractions.Messaging;

namespace Bookify.Services.Booking.Application.Bookings.MarkAsPaid;

public sealed record MarkBookingAsPaidCommand(Guid BookingId) : ICommand;
