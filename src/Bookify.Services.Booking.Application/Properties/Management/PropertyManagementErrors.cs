using Bookify.Services.Booking.Domain.Shared;

namespace Bookify.Services.Booking.Application.Properties.Management;

public static class PropertyManagementErrors
{
    public static readonly Error InvalidPropertyId = Error.Validation(
        "Property.Invalid",
        "The property identifier must not be empty.");

    public static Error NotFound(Guid propertyId) => Error.NotFound(
        "Property.NotFound",
        $"The property with ID '{propertyId}' was not found.");

}
