using Bookify.Services.Booking.Domain.Shared;

namespace Bookify.Services.Booking.Application.Availability.Get;

public static class GetAvailabilityErrors
{
    public static readonly Error InvalidPropertyId =
        Error.Validation(
            "Availability.InvalidPropertyId",
            "The property identifier cannot be empty.");

    public static readonly Error CheckInDateRequired =
        Error.Validation(
            "Availability.CheckInDateRequired",
            "The check-in date is required.");

    public static readonly Error CheckOutDateRequired =
        Error.Validation(
            "Availability.CheckOutDateRequired",
            "The check-out date is required.");

    public static readonly Error GuestCountRequired =
        Error.Validation(
            "Availability.GuestCountRequired",
            "The guest count is required.");

    public static readonly Error InvalidGuestCount =
        Error.Validation(
            "Availability.InvalidGuestCount",
            "The guest count must be greater than zero.");

    public static Error PropertyNotFound(Guid propertyId) =>
        Error.NotFound(
        "Property.NotFound",
        $"The property with identifier '{propertyId}' was not found.");

    public static Error PropertyInactive(Guid propertyId) =>
        Error.Conflict(
            "Availability.PropertyInactive",
            $"The property with identifier '{propertyId}' is inactive and " +
            $"cannot receive availability request.");

}
