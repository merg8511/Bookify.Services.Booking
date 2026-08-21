using Bookify.Services.Booking.Api.Endpoints.Bookings.Approve;
using Bookify.Services.Booking.Api.Endpoints.Bookings.Cancel;
using Bookify.Services.Booking.Api.Endpoints.Bookings.Complete;
using Bookify.Services.Booking.Api.Endpoints.Bookings.Create;
using Bookify.Services.Booking.Api.Endpoints.Bookings.ExpirePayment;
using Bookify.Services.Booking.Api.Endpoints.Bookings.MarkAsPaid;
using Bookify.Services.Booking.Api.Endpoints.Bookings.Reject;

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
        ApproveBookingEndpoint.Map(bookingsGroup);
        RejectBookingEndpoint.Map(bookingsGroup);
        MarkBookingAsPaidEndpoint.Map(bookingsGroup);
        ExpireBookingPaymentEndpoint.Map(bookingsGroup);
        CompleteBookingEndpoint.Map(bookingsGroup);
        CancelBookingEndpoint.Map(bookingsGroup);
    }

    internal static string GetResourceLocation(Guid bookingId)
    {
        return
            $"{ApiRoutePrefixes.V1}" +
            $"{RoutePrefix}/" +
            $"{bookingId}";
    }
}
