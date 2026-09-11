using Bookify.Services.Booking.Application.Abstractions.Payments;
using Bookify.Services.Booking.Domain.Shared;

namespace Bookify.Services.Booking.Application.Payments.Cancellation;

public static class PaymentCancellationErrors
{
    public static Error PaymentStateUncertain(Guid paymentId) =>
        Error.Failure(
            "Payments.Cancellation.StateUncertain",
            $"Payment '{paymentId}' is pending but no active " +
            $"payment attempt is available to verify or cancel.");

    public static Error InconsistentSucceededState(Guid paymentId) =>
        Error.Failure(
            "Payments.Cancellation.InconsistentSucceededState",
            $"Payment '{paymentId}' is succeeded but no succeeded " +
            $"payment attempt is available for reconciliation.");

    public static Error ProviderReferenceMismatch(
        string expectedExternalReference,
        string actualExternalReference) =>
        Error.Failure(
            "Payments.Cancellation.ProviderReferenceMismatch",
            $"The payment provider returned external reference " +
            $"'{actualExternalReference}', but" +
            $"'{expectedExternalReference}' was expected.");

    public static Error CancellationNotConfirmed(
        string externalReference,
        PaymentGatewayStatus status) =>
        Error.Failure(
            "Payments.Cancellation.NotConfirmed",
            $"Cancellation of provider payment " +
            $"'{externalReference}' was not confirmed. " +
            $"The observed provider status is '{status}'.");
}
