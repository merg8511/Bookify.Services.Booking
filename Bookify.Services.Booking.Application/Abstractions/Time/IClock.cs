namespace Bookify.Services.Booking.Application.Abstractions.Time;

public interface IClock
{
    public DateTimeOffset UtcNow { get; }
}
