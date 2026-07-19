using Bookify.Services.Booking.Domain.Shared;

namespace Bookify.Services.Booking.Domain.Bookings.Errors
{
    public static class BookingErrors
    {
        public static readonly Error InvalidGuestCount = Error.Validation(
            "Booking.InvalidGuestCount",
            "The booking must include at least one guest.");

        public static readonly Error GuestCapacityExceeded = Error.Validation(
            "Booking.GuestCapacityExceeded",
            "The number of guests exceeds the maximum capacity of the rentable unit.");

        public static readonly Error RentableUnitInactive = Error.Conflict(
            "Booking.RentableUnitInactive",
            "The selected rentable unit is not active and cannot be booked");

        public static Error InvalidStatusTransition(
            BookingStatus currentStatus,
            BookingStatus targeStatus) =>
            Error.Conflict(
                "Booking.InvalidStatusTransition",
                $"A booking in status '{currentStatus}' cannot transition to '{targeStatus}'");
    }
}
