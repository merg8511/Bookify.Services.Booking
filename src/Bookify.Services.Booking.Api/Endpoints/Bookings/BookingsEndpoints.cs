using Bookify.Services.Booking.Api.Endpoints.Bookings.Create;

namespace Bookify.Services.Booking.Api.Endpoints.Bookings;

internal static class BookingsEndpoints
{
    internal const string RoutePrefix = "/bookings";

    public static void Map(RouteGroupBuilder apiGroup)
    {
        RouteGroupBuilder bookingsGroup =
            apiGroup
                .MapGroup(RoutePrefix)
                .WithTags("Bookings");

        CreateBookingEndpoint.Map(bookingsGroup);
    }

    internal static string GetResourceLocation(Guid bookingId)
    {
        return
            $"{ApiRoutePrefixes.V1}" +
            $"{RoutePrefix}/" +
            $"{bookingId}";
    }
}
