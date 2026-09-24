using Bookify.Services.Booking.Domain.Shared;

namespace Bookify.Services.Booking.Domain.Bookings.Errors;

public static class BookingDeadlineErrors
{
    public static readonly Error ApprovalDeadlineAlreadyScheduled =
        Error.Conflict(
            "Booking.ApprovalDeadlineAlreadyScheduled",
            "The approval deadline has already been scheduled.");

    public static readonly Error InvalidApprovalDeadline =
        Error.Validation(
            "Booking.InvalidApprovalDeadline",
            "The approval deadline must be after the booking creation time.");

    public static readonly Error PaymentDeadlineAlreadyScheduled =
        Error.Conflict(
            "Booking.PaymentDeadlineAlreadyScheduled",
            "The payment deadline has already been scheduled.");

    public static readonly Error InvalidPaymentDeadline =
        Error.Validation(
            "Booking.InvalidPaymentDeadline",
            "The payment deadline must be after the booking approval time.");

    public static Error InvalidStatusForApprovalDeadline(BookingStatus status) =>
        Error.Conflict(
            "Booking.InvalidStatusForApprovalDeadline",
            $"An approval deadlien cannot be scheduled while booking is in status '{status}'.");

    public static Error InvalidStatusForPaymentDeadline(BookingStatus status) =>
        Error.Conflict(
            "Booking.InvalidStatusForPaymentDeadline",
            $"A payment deadline cannot be scheduled while the booking is in status '{status}'");
}
