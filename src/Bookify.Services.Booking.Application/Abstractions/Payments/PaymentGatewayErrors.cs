using Bookify.Services.Booking.Domain.Shared;

namespace Bookify.Services.Booking.Application.Abstractions.Payments;

public static class PaymentGatewayErrors
{
    public static readonly Error ExternalReferenceRequired =
        Error.Validation(
            "Payments.Gateway.ExternalReferenceRequired",
            "The external payment reference is required.");

    public static readonly Error IdempotencyKeyRequired =
        Error.Validation(
            "Payments.Gateway.IdempotencyKeyRequired",
            "The payment operation idempotency key is required.");

    public static readonly Error AmountMustBePositive =
        Error.Validation(
            "Payments.Gateway.AmountMustBePositive",
            "The payment amount must be greater than zero.");

    public static readonly Error ProviderRejected =
        Error.Failure(
            "Payments.Gateway.ProviderRejected",
            "The payment provider rejected the payment request.");

    public static readonly Error ProviderTimeout =
        Error.Failure(
            "Payments.Gateway.ProviderTimeout",
            "The payment provider did not respond within the expected time.");

    public static readonly Error ProviderUnavailable =
        Error.Failure(
            "Payments.Gateway.ProviderUnavailable",
            "The payment provider is temporarily unavailable.");

    public static Error InvalidAmountPrecision(
        string currency) =>
        Error.Validation(
            "Payments.Gateway.InvalidAmountPrecision",
            $"The payment amount has an invalid precision for currency '{currency}'.");

    public static Error AmountOutOfRange(
        string currency) =>
        Error.Validation(
            "Payments.Gateway.AmountOutOfRange",
            $"The payment amount is outside the supported range for currency '{currency}'.");

    public static Error ExternalReferenceNotFound(
        string externalReference) =>
        Error.NotFound(
            "Payments.Gateway.ExternalReferenceNotFound",
            $"External payment reference '{externalReference}' was not found.");

    public static Error CannotCancel(
        string externalReference,
        PaymentGatewayStatus status) =>
        Error.Conflict(
            "Payments.Gateway.CannotCancel",
            $"External payment reference '{externalReference}' cannot be cancelled while" +
            $"it is {status}");

    public static Error UnsupportedProviderStatus(
        string status) =>
        Error.Failure(
            "Payments.Gateway.UnsupportedProviderStatus",
            $"The payment provider returned unsupported status '{status}'");
}
