using Bookify.Services.Booking.Domain.Shared;

namespace Bookify.Services.Booking.Application.Bookings.Complete;

public static class CompleteBookingErrors
{
    public static readonly Error InvalidBookingId =
        Error.Validation(
            "Booking.InvalidId",
            "The booking identifier must not be empty.");

    public static Error NotFound(Guid bookingId) =>
        Error.NotFound(
            "Booking.NotFound",
            $"The booking with ID '{bookingId}' was not found.");
}
