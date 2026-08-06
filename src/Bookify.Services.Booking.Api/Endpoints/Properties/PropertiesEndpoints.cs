using Bookify.Services.Booking.Api.Endpoints.Properties.Create;
using Bookify.Services.Booking.Api.Endpoints.Properties.GetAvailability;
using Bookify.Services.Booking.Api.Endpoints.Properties.GetById;
using Bookify.Services.Booking.Api.Endpoints.Properties.GetPaged;

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

        GetPropertiesEndpoint.Map(
            propertiesGroup);

        CreatePropertyEndpoint.Map(
            propertiesGroup);

        GetPropertyByIdEndpoint.Map(
            propertiesGroup);

        GetAvailabilityEndpoint.Map(
            propertiesGroup);
    }
}
