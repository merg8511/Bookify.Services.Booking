namespace Bookify.Services.Booking.Infrastructure.DemoSeeding;

internal sealed class DemoSeedBatchRecord
{
    private DemoSeedBatchRecord() { }

    public string Key { get; private set; } = string.Empty;
    public DateTimeOffset AppliedAtUtc { get; private set; }
}
