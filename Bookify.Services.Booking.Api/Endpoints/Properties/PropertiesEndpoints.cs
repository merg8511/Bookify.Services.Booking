using Bookify.Services.Booking.Api.Endpoints.Properties.Create;
using Bookify.Services.Booking.Api.Endpoints.Properties.GetById;

namespace Bookify.Services.Booking.Api.Endpoints.Properties;

internal static class PropertiesEndpoints
{
    public static void Map(
        RouteGroupBuilder apiGroup)
    {
        RouteGroupBuilder propertiesGroup =
            apiGroup
                .MapGroup("/properties")
                .WithTags("Properties");

        CreatePropertyEndpoint.Map(
            propertiesGroup);

        GetPropertyByIdEndpoint.Map(
            propertiesGroup);
    }
}
