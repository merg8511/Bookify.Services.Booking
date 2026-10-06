namespace Bookify.Services.Booking.Api.Security;

internal enum EndpointAccessCategory
{
    Public,
    CustomerOrGuest,
    OwnerOrAdmin,
    ProviderWebhook
}
