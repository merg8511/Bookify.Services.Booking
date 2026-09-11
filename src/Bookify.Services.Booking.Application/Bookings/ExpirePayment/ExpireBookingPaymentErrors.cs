using Bookify.Services.Booking.Domain.Shared;

namespace Bookify.Services.Booking.Application.Bookings.ExpirePayment;

public static class ExpireBookingPaymentErrors
{
    public static readonly Error InvalidBookingId =
        Error.Validation(
            "Booking.InvalidId",
            "The booking identifier must not be empty.");

    public static Error NotFound(Guid bookingId) =>
        Error.NotFound(
            "Booking.NotFound",
            $"The booking with ID '{bookingId}' was not found.");

    public static Error PaymentAlreadySucceeded(Guid bookingId) =>
        Error.Conflict(
            "Booking.PaymentAlreadySucceeded",
            $"Booking '{bookingId}' cannot expire because " +
            $"its payment has already succeeded.");
}
