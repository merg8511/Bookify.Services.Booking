using Bookify.Services.Booking.Domain.Shared;

namespace Bookify.Services.Booking.Application.RentableUnits.Management;

public static class RentableUnitManagementErrors
{
    public static readonly Error InvalidPropertyId = Error.Validation(
        "Property.InvalidId",
        "The property identifier must not be empty.");

    public static readonly Error InvalidUnitId = Error.Validation(
        "RentableUnit.InvalidId",
        "The rentable unit identifier must not be empty.");

    public static Error PropertyNotFound(Guid propertyId) => Error.NotFound(
        "Property.NotFound",
        $"The property with ID '{propertyId}' was not found.");

    public static Error UnitNotFound(Guid unitId) => Error.NotFound(
        "RentableUnit.NotFound",
        $"The rentable unit with ID '{unitId}' was not found in the requested property.");

    public static readonly Error TypeChangeHasBookings = Error.Conflict(
        "RentableUnit.TypeChangeHasBookings",
        "The rentable unit type cannot be changed because bookings reference this unit.");

    public static readonly Error CapacityBelowBookedGuestCount = Error.Conflict(
        "RentableUnit.CapacityBelowBookedGuestCount",
        "The new maximum capacity is lower than guest count of an existing booking.");
}
