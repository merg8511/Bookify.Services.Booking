namespace Bookify.Services.Booking.Api.BackgroundJobs;

public sealed class BackgroundJobsOptions
{
    public const string SectionName = "BackgroundJobs";
    public bool Enabled { get; set; } = true;
    public TimeSpan PollingInterval { get; set; } = TimeSpan.FromSeconds(10);
    public int BatchSize { get; set; } = 50;
}
