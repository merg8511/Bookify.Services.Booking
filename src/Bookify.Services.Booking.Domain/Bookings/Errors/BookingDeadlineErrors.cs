using Bookify.Services.Booking.Domain.Shared;

namespace Bookify.Services.Booking.Domain.Bookings.Errors;

public static class BookingDeadlineErrors
{
    public static readonly Error InvalidApprovalDeadline =
        Error.Validation(
            "Booking.InvalidApprovalDeadline",
            "The approval deadline must be after the booking creation time.");

    public static readonly Error InvalidPaymentDeadline =
        Error.Validation(
            "Booking.InvalidPaymentDeadline",
            "The payment deadline must be after the booking approval time.");
}
