using Bookify.Services.Booking.Application.Abstractions.Messaging;

namespace Bookify.Services.Booking.Application.Bookings.ExpirePayment;

public sealed record ExpireBookingPaymentCommand(
   Guid BookingId) : ICommand;
