using Bookify.Services.Booking.Domain.Shared;

namespace Bookify.Services.Booking.Domain.Bookings.Errors;

public static class BookingReferenceErrors
{
    public static readonly Error Required =
        Error.Validation(
            "Booking.ReferenceRequired",
            "The booking reference is required.");

    public static readonly Error InvalidFormat =
        Error.Validation(
            "Booking.ReferenceInvalidFormat",
            "The booking reference format is invalid.");
}
