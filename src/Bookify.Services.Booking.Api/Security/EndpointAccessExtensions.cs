using Bookify.Services.Booking.Application.Abstractions.Security;

namespace Bookify.Services.Booking.Api.Security;

internal static class EndpointAccessExtensions
{
    public static RouteHandlerBuilder AllowPublicAccess(this RouteHandlerBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder
            .WithMetadata(new EndpointAccessMetadata(EndpointAccessCategory.Public))
            .AllowAnonymous();
    }

    public static RouteHandlerBuilder ClassifyCustomerOrGuestAccess(this RouteHandlerBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder
            .WithMetadata(new EndpointAccessMetadata(EndpointAccessCategory.CustomerOrGuest));
    }

    public static RouteHandlerBuilder RequireOwnerOrAdminAccess(this RouteHandlerBuilder builder)
    {
        return builder
            .WithMetadata(new EndpointAccessMetadata(EndpointAccessCategory.OwnerOrAdmin))
            .RequireAuthorization(policy =>
                policy.RequireRole(BookifyRoles.Owner, BookifyRoles.Admin));
    }

    public static RouteHandlerBuilder AllowProviderWebHookAccess(this RouteHandlerBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder
            .WithMetadata(new EndpointAccessMetadata(EndpointAccessCategory.ProviderWebhook))
            .AllowAnonymous();
    }
}
