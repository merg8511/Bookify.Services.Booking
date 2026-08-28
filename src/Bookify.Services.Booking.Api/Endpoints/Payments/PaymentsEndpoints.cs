using Bookify.Services.Booking.Api.Endpoints.Payments.Initiate;

namespace Bookify.Services.Booking.Api.Endpoints.Payments;

internal static class PaymentsEndpoints
{
    internal const string RoutePrefix = "/payments";

    public static void Map(RouteGroupBuilder apiGroup)
    {
        RouteGroupBuilder paymentsGroup =
            apiGroup
                .MapGroup(RoutePrefix)
                .WithTags("Payments");

        InitiatePaymentEndpoint.Map(paymentsGroup);
    }
}
