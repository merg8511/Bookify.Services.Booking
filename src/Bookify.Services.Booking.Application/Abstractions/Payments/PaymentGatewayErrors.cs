using Bookify.Services.Booking.Domain.Shared;

namespace Bookify.Services.Booking.Application.Abstractions.Payments;

public static class PaymentGatewayErrors
{
    public static readonly Error ExternalReferenceRequired =
        Error.Validation(
            "Payments.Gateway.ExternalReferenceRequired",
            "The external payment reference is required.");

    public static readonly Error ProviderRejected =
        Error.Failure(
            "Payments.Gateway.ProviderRejected",
            "The payment provider rejected the payment request.");

    public static readonly Error ProviderTimeout =
        Error.Failure(
            "Payments.Gateway.ProviderTimeout",
            "The payment provider did not respond within the expected time.");

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
}
