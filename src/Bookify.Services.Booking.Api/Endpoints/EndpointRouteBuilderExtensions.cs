using Bookify.Services.Booking.Api.Endpoints.Bookings;
using Bookify.Services.Booking.Api.Endpoints.Properties;

namespace Bookify.Services.Booking.Api.Endpoints;

internal static class EndpointRouteBuilderExtensions
{
    public static IEndpointRouteBuilder MapApiEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        RouteGroupBuilder apiGroup = endpoints.MapGroup(ApiRoutePrefixes.V1);

        PropertiesEndpoints.Map(apiGroup);

        BookingsEndpoints.Map(apiGroup);

        return endpoints;
    }
}
