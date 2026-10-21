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
        internal const string Update = "Properties.Update";
        internal const string SetStatus = "Properties.SetStatus";
    }

    internal static class Bookings
    {
        internal const string Create = "Bookings.Create";
        internal const string Approve = "Bookings.Approve";
        internal const string Reject = "Bookings.Reject";
        internal const string Cancel = "Bookings.Cancel";
        internal const string GetByIdentifier = "Bookings.GetByIdentifier";
    }

    internal static class Payments
    {
        internal const string Initiate = "Payments.Initiate";
        internal const string StripeWebhook = "Payments.StripeWebhook";
        internal const string GetStatus = "Payments.GetStatus";
    }

    internal static class AdminRentableUnits
    {
        internal const string Create = "AdminRentableUnits.Create";
        internal const string List = "AdminRentableUnits.List";
        internal const string GetById = "AdminRentableUnits.GetById";
        internal const string Update = "AdminRentableUnits.Update";
        internal const string SetStatus = "AdminRentableUnits.SetStatus";
        internal const string GetPricing = "AdminRentableUnits.GetPricing";
        internal const string ConfigurePricing = "AdminRentableUnits.ConfigurePricing";
    }
}
