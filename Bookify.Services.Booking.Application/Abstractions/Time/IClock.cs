namespace Bookify.Services.Booking.Application.Abstractions.Time
{
    public interface IClock
    {
        DateTimeOffset UtcNow { get; }
    }
}
