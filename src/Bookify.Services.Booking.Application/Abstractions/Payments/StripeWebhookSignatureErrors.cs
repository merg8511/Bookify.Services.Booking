using Bookify.Services.Booking.Domain.Shared;

namespace Bookify.Services.Booking.Application.Abstractions.Payments;

public static class StripeWebhookSignatureErrors
{
    public static readonly Error SignatureRequired =
        Error.Validation(
            "Payments.Webhook.SignatureRequired",
            "The Stripe-Signature header is required.");

    public static readonly Error InvalidSignature =
        Error.Validation(
            "Payments.Webhook.InvalidSignature",
            "The Stripe webhook signature is invalid.");

    public static readonly Error WebhookSecretNotConfigured =
        Error.Failure(
            "Payments.Webhook.SecretNotConfigured",
            "The Stripe webhook signing secret is not configured.");
}
