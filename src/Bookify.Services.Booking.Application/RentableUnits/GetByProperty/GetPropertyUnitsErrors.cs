using Bookify.Services.Booking.Domain.Shared;

namespace Bookify.Services.Booking.Application.RentableUnits.GetByProperty;

public static class GetPropertyUnitsErrors
{
    public static readonly Error InvalidPropertyId =
        Error.Validation(
            "Property.InvalidId",
            "The property identifier must not be empty.");

    public static Error PropertyNotFound(Guid propertyId) =>
        Error.NotFound(
            "Property.NotFound",
            $"The property with ID '{propertyId}' was not found.");

    public static Error PropertyInactive(Guid propertyId) =>
        Error.Conflict(
            "Property.Inactive",
            $"The property with ID '{propertyId}' is inactive.");
}
