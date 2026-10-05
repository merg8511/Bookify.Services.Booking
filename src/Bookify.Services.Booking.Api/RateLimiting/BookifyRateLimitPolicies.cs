namespace Bookify.Services.Booking.Api.RateLimiting;

public class BookifyRateLimitPolicies
{
    public const string PublicReads = "public-reads";
    public const string BookingCreation = "booking-creation";
    public const string PaymentInitiation = "payment-initiation";
    public const string LoginFacing = "login-facing";
    public const string Webhook = "webhook";
}
