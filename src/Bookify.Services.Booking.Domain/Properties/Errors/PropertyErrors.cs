using Bookify.Services.Booking.Domain.Shared;

namespace Bookify.Services.Booking.Domain.Properties.Errors;

public static class PropertyErrors
{
    public static readonly Error InvalidName = Error.Validation(
        "Property.InvalidName",
        "The property name cannot be empty");

    public static readonly Error InvalidTimeZoneId = Error.Validation(
        "Property.InvalidTimeZoneId",
        "The property time zone identifier cannot be empty");
}
