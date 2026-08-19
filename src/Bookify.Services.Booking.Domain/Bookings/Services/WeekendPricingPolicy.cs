namespace Bookify.Services.Booking.Domain.Bookings.Services;

public static class WeekendPricingPolicy
{
    public static bool IsWeekendNight(DateOnly date)
    {
        return date.DayOfWeek is
            DayOfWeek.Friday or DayOfWeek.Saturday;
    }
}
