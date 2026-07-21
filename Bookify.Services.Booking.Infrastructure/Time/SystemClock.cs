using Bookify.Services.Booking.Application.Abstractions.Time;

namespace Bookify.Services.Booking.Infrastructure.Time;

internal sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow =>
        DateTimeOffset.UtcNow;
}
