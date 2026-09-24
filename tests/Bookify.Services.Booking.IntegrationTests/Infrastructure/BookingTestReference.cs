namespace Bookify.Services.Booking.IntegrationTests.Infrastructure;

internal static class BookingTestReference
{
    public static string From(Guid bookingId)
    {
        string token = bookingId.ToString("N").ToUpperInvariant();

        return
            $"BK-" +
            $"{token[0..4]}-" +
            $"{token[4..8]}-" +
            $"{token[8..12]}-" +
            $"{token[12..16]}-" +
            $"{token[16..20]}";
    }
}
