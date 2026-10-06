namespace Bookify.Services.Booking.Infrastructure.Payments.Stripe;

internal sealed class StripePaymentOptions
{
    public const string SectionName = "Payments:Stripe";
    public string SecretKey { get; set; } = string.Empty;
    public string WebhookSecret { get; set; } = string.Empty;
    public long WebhookToleranceSeconds { get; set; } = 300;
}
