using Bookify.Services.Booking.Domain.Shared;

namespace Bookify.Services.Booking.Application.Payments.Webhooks.Stripe;

public static class StripeWebhookErrors
{
    public static readonly Error InvalidPayload =
        Error.Validation(
            "Payments.Webhook.InvalidPayload",
            "The Stripe webhook payload is invalid.");

    public static Error InvalidBookingId(
        string value) =>
        Error.Validation(
            "Payments.Webhook.InvalidBookingId",
            $"The webhook booking identifier '{value}' is invalid.");

    public static Error BookingNotFound(
        Guid bookingId) =>
        Error.Failure(
            "Payments.Webhook.BookingNotFound",
            $"Booking '{bookingId}' referenced by the webhook was not found.");

    public static Error PaymentNotFound(
        Guid bookingId) =>
        Error.Failure(
            "Payments.Webhook.PaymentNotFound",
            $"No payment exists for booking '{bookingId}'.");

    public static Error PaymentAttemptNotFound(
        Guid paymentId,
        string externalReference) =>
        Error.Failure(
            "Payments.Webhook.PaymentAttemptNotFound",
            $"Payment '{paymentId}' does not contain an attempt " +
            $"with external reference '{externalReference}'.");
}
