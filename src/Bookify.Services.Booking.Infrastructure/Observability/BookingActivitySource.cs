using System.Diagnostics;

namespace Bookify.Services.Booking.Infrastructure.Observability;

internal static class BookingActivitySource
{
    public const string Name = "Bookify.Services.Booking";
    public static readonly ActivitySource Instance = new(Name);
}
