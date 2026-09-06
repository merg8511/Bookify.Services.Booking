using Bookify.Services.Booking.Application.Abstractions.Payments;

namespace Bookify.Services.Booking.Application.Payments.Webhooks.Stripe;

public static class StripeWebhookEventTypes
{
    public const string PaymentIntentSucceeded = "payment_intent.succeeded";
    public const string PaymentIntentPaymentFailed = "payment_intent.payment_failed";
    public const string PaymentIntentCanceled = "payment_intent.canceled";

    public static bool TryMapToGatewayStatus(string eventType, out PaymentGatewayStatus status)
    {
        switch (eventType)
        {
            case PaymentIntentSucceeded:
                status = PaymentGatewayStatus.Succeeded;
                return true;

            case PaymentIntentPaymentFailed:
                status = PaymentGatewayStatus.Failed;
                return true;

            case PaymentIntentCanceled:
                status = PaymentGatewayStatus.Cancelled;
                return true;

            default:
                status = default;
                return false;
        }
    }
}
