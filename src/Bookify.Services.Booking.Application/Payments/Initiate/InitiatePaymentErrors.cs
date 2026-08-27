using Bookify.Services.Booking.Domain.Bookings;
using Bookify.Services.Booking.Domain.Shared;

namespace Bookify.Services.Booking.Application.Payments.Initiate;

public static class InitiatePaymentErrors
{
    public static readonly Error IdempotencyKeyRequired =
        Error.Validation(
            "Payments.Initiate.IdempotencyKeyRequired",
            "An idempotency key is required to initiate a payment.");

    public static Error BookingNotFound(
        Guid bookingId) =>
        Error.NotFound(
            "Payments.Initiate.BookingNotFound",
            $"Booking '{bookingId}' was not found.");

    public static Error BookingNotPendingPayment(
        BookingStatus currentStatus) =>
        Error.Conflict(
            "Payments.Initiate.BookingNotPendingPayment",
            $"Payment cannot be initiated while the booking is {currentStatus}.");

    public static readonly Error PaymentAlreadySucceeded =
        Error.Conflict(
            "Payments.Initiate.PaymentAlreadySucceeded",
            "The booking payment has already succeeded.");

    public static readonly Error PaymentCancelled =
        Error.Conflict(
            "Payments.Initiate.PaymentCancelled",
            "The booking payment has been cancelled.");

    public static Error PriceSnapshotMissing(
        Guid bookingId) =>
        Error.Conflict(
            "Payments.Initiate.PriceSnapshotMissing",
            $"The booking with identifier '{bookingId}' does not have " +
            $"a price snapshot and payment cannot be initiated.");
}
