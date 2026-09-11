using Bookify.Services.Booking.Domain.Shared;

namespace Bookify.Services.Booking.Application.Payments.Webhooks;

public static class PaymentWebhookPersistenceErrors
{
    public static Error EventIdentityMismatch(string eventId) =>
        Error.Failure(
            "Payments.Webhook.EventIdentityMismatch",
            $"Webhook event '{eventId}' already exists " +
            $"with different provider or event type metadata.");

    public static Error EventAlreadyProcessing(string eventId) =>
        Error.Failure(
            "Payments.Webhook.EventAlreadyProcessing",
            $"Webhook event '{eventId}' is already being processed.");
}
