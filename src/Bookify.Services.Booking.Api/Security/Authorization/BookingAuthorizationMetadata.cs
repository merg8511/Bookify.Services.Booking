namespace Bookify.Services.Booking.Api.Security.Authorization;

internal enum BookingAuthorizationSource
{
    RouteBookingId,
    RouteIdentifier,
    JsonBodyBookingId
}
internal sealed record BookingAuthorizationMetadata(BookingAuthorizationSource Source, string ValueName)
{
    public static BookingAuthorizationMetadata FromRouteBookingId(string valueName)
    {
        return new BookingAuthorizationMetadata(BookingAuthorizationSource.RouteBookingId, valueName);
    }

    public static BookingAuthorizationMetadata FromRouteIdentifier(string valueName)
    {
        return new BookingAuthorizationMetadata(BookingAuthorizationSource.RouteIdentifier, valueName);
    }

    public static BookingAuthorizationMetadata FromJsonBodyBookingId(string valueName)
    {
        return new BookingAuthorizationMetadata(BookingAuthorizationSource.JsonBodyBookingId, valueName);
    }
}
