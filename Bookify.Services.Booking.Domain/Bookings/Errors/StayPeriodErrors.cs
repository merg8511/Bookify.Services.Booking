using Bookify.Services.Booking.Domain.Shared;

namespace Bookify.Services.Booking.Domain.Bookings.Errors
{
    public static class StayPeriodErrors
    {
        public static readonly Error InvalidDateRange = Error.Validation(
            "StayPeriod.InvalidDateRange",
            "The check-out date must be after the check-in date.");
    }
}
