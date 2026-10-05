using Bookify.Services.Booking.Api.Security.Authorization;
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

    public static RouteHandlerBuilder RequireOwnerOrAdminAccess(this RouteHandlerBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder
            .WithMetadata(new EndpointAccessMetadata(EndpointAccessCategory.OwnerOrAdmin))
            .RequireAuthorization(policy =>
                policy.RequireRole(BookifyRoles.Owner, BookifyRoles.Admin));
    }

    public static RouteHandlerBuilder RequireAdminOnlyAccess(this RouteHandlerBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder.RequireAuthorization(BookifyAuthorizationPolicies.AdminOnly);
    }

    public static RouteHandlerBuilder RequirePropertyOwnerOrAdminBookingAccess(
        this RouteHandlerBuilder builder,
        string routeValueName)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(routeValueName);

        return builder
            .WithMetadata(new EndpointAccessMetadata(EndpointAccessCategory.OwnerOrAdmin))
            .WithMetadata(BookingAuthorizationMetadata.FromRouteBookingId(routeValueName))
            .RequireAuthorization(BookifyAuthorizationPolicies.PropertyOwnerOrAdmin);
    }

    public static RouteHandlerBuilder RequireCustomerOrGuestBookingAccessFromRoute(
        this RouteHandlerBuilder builder,
        string routeValueName)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(routeValueName);

        return RequireCustomerOrGuestBookingAccess(
            builder,
            BookingAuthorizationMetadata.FromRouteBookingId(routeValueName));
    }

    public static RouteHandlerBuilder RequireCustomerOrGuestBookingAccessFromIdentifier(
        this RouteHandlerBuilder builder,
        string routeValueName)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(routeValueName);

        return RequireCustomerOrGuestBookingAccess(
            builder,
            BookingAuthorizationMetadata.FromRouteIdentifier(routeValueName));
    }

    public static RouteHandlerBuilder RequireCustomerOrGuestBookingAccessFromJsonBody(
        this RouteHandlerBuilder builder,
        string propertyName)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(propertyName);

        return RequireCustomerOrGuestBookingAccess(
            builder,
            BookingAuthorizationMetadata.FromJsonBodyBookingId(propertyName));
    }

    public static RouteHandlerBuilder AllowProviderWebHookAccess(this RouteHandlerBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder
            .WithMetadata(new EndpointAccessMetadata(EndpointAccessCategory.ProviderWebhook))
            .AllowAnonymous();
    }

    private static RouteHandlerBuilder RequireCustomerOrGuestBookingAccess(
        RouteHandlerBuilder builder,
        BookingAuthorizationMetadata metadata)
    {
        return builder
            .WithMetadata(new EndpointAccessMetadata(EndpointAccessCategory.CustomerOrGuest))
            .WithMetadata(metadata)
            .RequireAuthorization(BookifyAuthorizationPolicies.CustomerOrGuestBookingAccess);
    }
}
