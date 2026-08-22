using Bookify.Services.Booking.Domain.Shared;

namespace Bookify.Services.Booking.Domain.Payments.Errors;

public static class PaymentAttemptErrors
{
    public static readonly Error ExternalReferenceRequired =
        Error.Validation(
            "Payments.Attempts.ExternalReferenceRequired",
            "The payment attempt external reference is required.");

    public static readonly Error AmountMustBePositive =
        Error.Validation(
            "Payments.Attempts.AmountMustBePositive",
            "The payment attempt amount must be greater than zero.");

    public static readonly Error CompletionBeforeCreation =
        Error.Validation(
            "Payments.Attempts.CompletionBeforeCreation",
            "The payment attempt cannot complete before it was created.");

    public static Error InvalidStatusTransition(
        PaymentAttemptStatus currentStatus,
        PaymentAttemptStatus targetStatus) =>
        Error.Conflict(
            "Payments.Attempts.InvalidStatusTransition",
            $"Payment attempt cannot transition from {currentStatus} to {targetStatus}.");
}
