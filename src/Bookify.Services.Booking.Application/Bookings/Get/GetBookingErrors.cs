using Bookify.Services.Booking.Domain.Shared;

namespace Bookify.Services.Booking.Application.Bookings.Get;

public static class GetBookingErrors
{
    public static readonly Error InvalidIdentifier =
        Error.Validation(
            "Booking.InvalidIdentifier",
            "The booking identifier must be a valid booking ID or booking reference.");

    public static Error NotFound(string identifier) =>
        Error.NotFound(
            "Booking.NotFound",
            $"The booking '{identifier}' was not found.");
}
