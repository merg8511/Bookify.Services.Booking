namespace Bookify.Services.Booking.Api.Timeouts;

internal sealed class BookifyRequestTimeoutOptions
{
    public const string SectionName = "RequestTimeouts";
    public TimeSpan Default { get; set; } = TimeSpan.FromSeconds(30);
    public TimeSpan PaymentInitiation { get; set; } = TimeSpan.FromSeconds(60);
    public TimeSpan StripeWebhook { get; set; } = TimeSpan.FromSeconds(45);
}
