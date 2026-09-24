namespace Bookify.Services.Booking.Api.Endpoints;

internal static class EndpointNames
{
    internal static class Properties
    {
        internal const string Create = "Properties.Create";
        internal const string GetById = "Properties.GetById";
        internal const string List = "Properties.List";
        internal const string GetAvailability = "Properties.GetAvailability";
        internal const string GetUnits = "Properties.GetUnits";
    }

    internal static class Bookings
    {
        internal const string Create = "Bookings.Create";
        internal const string Approve = "Bookings.Approve";
        internal const string Reject = "Bookings.Reject";
        internal const string ExpirePayment = "Bookings.ExpirePayment";
        internal const string Complete = "Bookings.Complete";
        internal const string Cancel = "Bookings.Cancel";
        internal const string GetByIdentifier = "Bookings.GetByIdentifier";
    }

    internal static class Payments
    {
        internal const string Initiate = "Payments.Initiate";
        internal const string StripeWebhook = "Payments.StripeWebhook";
        internal const string GetStatus = "Payments.GetStatus";
    }
}
