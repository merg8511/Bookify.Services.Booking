using Bookify.Services.Booking.Api.Endpoints.Payments.GetStatus;
using Bookify.Services.Booking.Api.Endpoints.Payments.Initiate;
using Bookify.Services.Booking.Api.Endpoints.Payments.Webhooks;

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
        StripePaymentWebhookEndpoint.Map(paymentsGroup);
        GetPaymentStatusEndpoint.Map(paymentsGroup);
    }
}
