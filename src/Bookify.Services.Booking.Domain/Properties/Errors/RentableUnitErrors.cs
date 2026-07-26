using Bookify.Services.Booking.Domain.Shared;

namespace Bookify.Services.Booking.Domain.Properties.Errors;

public static class RentableUnitErrors
{
    public static readonly Error InvalidPropertyId = Error.Validation(
        "RentableUnit.InvalidPropertyId",
        "The rentable unit must belong to a valid property.");

    public static readonly Error InvalidName = Error.Validation(
        "RentableUnit.InvalidName",
        "The rentable unit name cannot be empty.");

    public static readonly Error InvalidType = Error.Validation(
        "RentableUnit.InvalidType",
        "The rentable unit type is invalid.");

    public static readonly Error InvalidMaximumCapacity = Error.Validation(
        "RentableUnit.InvalidMaximumCapacity",
        "The maximum capacity must be greater than zero.");

    public static readonly Error InvalidMaxBaseGuest = Error.Validation(
        "RentableUnit.InvalidMaxBaseGuest",
        "The number of guest included in the base rate must be greater than zero.");

    public static readonly Error BaseGuestsExceedCapacity = Error.Validation(
        "RentableUnit.BaseGuestsExceedCapacity",
        "The number of guests included in the base rate cannot exceed the maximum capacity.");
}
