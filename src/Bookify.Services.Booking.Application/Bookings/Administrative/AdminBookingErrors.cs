using Bookify.Services.Booking.Domain.Shared;

namespace Bookify.Services.Booking.Application.Bookings.Administrative;

public static class AdminBookingErrors
{
    public static readonly Error InvalidPropertyId = Error.Validation(
        "Property.InvalidId",
        "The property identifier must not be empty.");

    public static readonly Error InvalidBookingId = Error.Validation(
        "Booking.InvalidId",
        "The booking identifier must not be empty.");

    public static readonly Error InvalidStatus = Error.Validation(
        "AdminBookings.InvalidStatus",
        "The booking status filter is invalid.");

    public static readonly Error InvalidDateRange = Error.Validation(
        "AdminBookings.InvalidDateRange",
        "RangeStart and RangeEnd must both be provider, and RangeEnd must be after RangeStart.");

    public static readonly Error InvalidCustomerSubjectId = Error.Validation(
        "AdminBookings.InvalidCustomerSubjectId",
        "The customer subject identifier must contain between 1 and 255 characters witout surrounding whitspace.");

    public static Error PropertyNotFound(Guid propertyId) => Error.NotFound(
        "Property.NotFound",
        $"The property with ID '{propertyId}' was not found.");

    public static Error BookingNotFound(Guid bookingId) => Error.NotFound(
        "Booking.NotFound",
        $"The booking with ID '{bookingId}' was not found in the requested property.");

    public static readonly Error InvalidUnitId = Error.Validation(
        "RentableUnit.InvalidId",
        "The rentable unit filter must not be empty.");
}
