namespace Bookify.Services.Booking.Api.Timeouts;

internal static class BookifyRequestTimeoutPolicies
{
    public const string PaymentInitiation = "payment-initiation";
    public const string StripeWebhook = "stripe-webhook";
}
