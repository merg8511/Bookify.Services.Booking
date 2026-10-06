using Microsoft.Extensions.Primitives;

namespace Bookify.Services.Booking.Api.Security;

internal static class BookingGuestAccessHeader
{
    public const string Name = "Booking-Guest-Token";

    public static string? GetToken(HttpRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        StringValues values = request.Headers[Name];

        if (values.Count != 1)
        {
            return null;
        }

        string? value = values[0];

        return string.IsNullOrWhiteSpace(value) ? null : value;
    }
}
