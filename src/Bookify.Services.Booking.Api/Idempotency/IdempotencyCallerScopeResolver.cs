using Bookify.Services.Booking.Api.Security;

namespace Bookify.Services.Booking.Api.Idempotency;

internal static class IdempotencyCallerScopeResolver
{
    public static string Resolve(HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        EndpointAccessMetadata? metadata = httpContext
            .GetEndpoint()?.Metadata
            .GetMetadata<EndpointAccessMetadata>();

        string actorKey = metadata?.Category == EndpointAccessCategory.CustomerOrGuest
            ? RequestActorKeyResolver.ResolveBookingActorOrIp(httpContext)
            : RequestActorKeyResolver.ResolveIdentityOrIp(httpContext);

        return RequestActorKeyResolver.CreateFingerPrint(actorKey);
    }
}
