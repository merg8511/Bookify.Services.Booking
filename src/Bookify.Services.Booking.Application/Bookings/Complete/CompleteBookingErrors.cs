using Bookify.Services.Booking.Domain.Shared;

namespace Bookify.Services.Booking.Application.Bookings.Complete;

public static class CompleteBookingErrors
{
    public static readonly Error InvalidBookingId = Error.Validation(
        "Booking.InvalidId",
        "The booking identifier must not be empty.");

    public static Error NotFound(Guid bookingId) => Error.NotFound(
        "Booking.NotFound",
        $"The booking with ID '{bookingId}' was not found.");

    public static Error PropertyNotFound(Guid propertyId) => Error.Failure(
        "Booking.PropertyNotFound",
        $"The property with ID '{propertyId}' required to calculate booking checkout was not found.");

    public static Error InvalidPropertyTimeZone(Guid propertyId, string timeZoneId) => Error.Failure(
        "Booking.InvalidPropertyTimeZone",
        $"The time zone '{timeZoneId}' configured for property '{propertyId}' could not be used to calculate checkout.");
}
