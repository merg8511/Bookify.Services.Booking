using Bookify.Services.Booking.Api.Endpoints.Properties;

namespace Bookify.Services.Booking.Api.Endpoints;

internal static class EndpointRouteBuilderExtensions
{
    public static IEndpointRouteBuilder MapApiEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        RouteGroupBuilder apiGroup = endpoints.MapGroup("/api");

        PropertiesEndpoints.Map(apiGroup);

        return endpoints;
    }
}
