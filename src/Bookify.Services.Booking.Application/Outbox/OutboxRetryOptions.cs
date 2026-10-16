namespace Bookify.Services.Booking.Application.Outbox;

public sealed class OutboxRetryOptions
{
    public const string SectionName = "OutboxRetry";
    public int MaxAttempts { get; set; } = 5;
    public TimeSpan BaseDelay { get; set; } = TimeSpan.FromSeconds(30);
    public TimeSpan MaxDelay { get; set; } = TimeSpan.FromMinutes(15);
    public TimeSpan DisabledDelay { get; set; } = TimeSpan.FromMinutes(5);
}
