using Bookify.Services.Booking.Domain.Shared;

namespace Bookify.Services.Booking.Application.Bookings.Administrative.Calendar;

public static class AdminBookingCalendarErrors
{
    public static readonly Error InvalidDateRange = Error.Validation(
        "AdminCalendar.InvalidDateRange",
        "RangeStart and RangeEnd are required, and RangeEnd must be after RangeStart.");

    public static readonly Error RangeTooLarge = Error.Validation(
        "AdminCalendar.RangeTooLarge",
        "The requested calendar range cannot exceed 366 days.");
}
