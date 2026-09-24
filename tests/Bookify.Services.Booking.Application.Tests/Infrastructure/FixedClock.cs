using Bookify.Services.Booking.Application.Abstractions.Time;

namespace Bookify.Services.Booking.Application.Tests.Infrastructure;

internal sealed class FixedClock : IClock
{
    public FixedClock(DateTimeOffset utcNow)
    {
        UtcNow = utcNow;
    }

    public DateTimeOffset UtcNow { get; }
}
