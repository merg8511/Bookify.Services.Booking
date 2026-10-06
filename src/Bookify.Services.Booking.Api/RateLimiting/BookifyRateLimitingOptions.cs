namespace Bookify.Services.Booking.Api.RateLimiting;

internal sealed class BookifyRateLimitingOptions
{
    public const string SectionName = "RateLimiting";

    public RateLimitRuleOptions PublicReads { get; set; } = new()
    {
        PermitLimit = 120,
        Window = TimeSpan.FromMinutes(1)
    };

    public RateLimitRuleOptions BookingCreation { get; set; } = new()
    {
        PermitLimit = 10,
        Window = TimeSpan.FromMinutes(1)
    };

    public RateLimitRuleOptions PaymentInitiation { get; set; } = new()
    {
        PermitLimit = 6,
        Window = TimeSpan.FromMinutes(1)
    };

    public RateLimitRuleOptions LoginFacing { get; set; } = new()
    {
        PermitLimit = 5,
        Window = TimeSpan.FromMinutes(1)
    };

    public RateLimitRuleOptions Webhook { get; set; } = new()
    {
        PermitLimit = 300,
        Window = TimeSpan.FromMinutes(1)
    };
}

internal sealed class RateLimitRuleOptions
{
    public int PermitLimit { get; set; }
    public TimeSpan Window { get; set; }
}
