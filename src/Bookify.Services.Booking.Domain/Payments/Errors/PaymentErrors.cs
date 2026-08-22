using Bookify.Services.Booking.Domain.Shared;

namespace Bookify.Services.Booking.Domain.Payments.Errors;

public static class PaymentErrors
{
    public static readonly Error BookingIdRequired =
        Error.Validation(
            "Payments.BookingIdRequired",
            "The booking identifier is required.");

    public static readonly Error AmountMustBePositive =
        Error.Validation(
            "Payments.AmountMustBePositive",
            "The payment amount must be greater than zero.");

    public static readonly Error ActiveAttemptAlreadyExists =
        Error.Conflict(
            "Payments.ActiveAttemptAlreadyExists",
            "The payment already has a pending attempt.");

    public static readonly Error AttemptBeforePaymentCreation =
        Error.Validation(
            "Payments.AttemptBeforePaymentCreation",
            "A payment attempt cannot be created before the payment.");

    public static Error DuplicateExternalReference(
        string externalReference) =>
        Error.Conflict(
            "Payments.DuplicateExternalReference",
            $"The extenal payment reference '{externalReference}' already exists.");

    public static Error AttemptNotFound(
        string externalReference) =>
        Error.NotFound(
            "Payments.AttemptNotFound",
            $"Payment attempt '{externalReference}' was not found.");

    public static Error CannotAddAttempt(
        PaymentStatus status) =>
        Error.Conflict(
            "Payments.CannotAddAttempt",
            $"A new payment attempt cannot be added while the payment is {status}");
}
