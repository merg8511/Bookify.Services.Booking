using Bookify.Services.Booking.Domain.Bookings.ValueObjects;
using Bookify.Services.Booking.Domain.Shared;
using Bookify.Services.Booking.Domain.Shared.ValueObjects;

namespace Bookify.Services.Booking.Domain.Bookings.Services;

public static class BookingPricingEngine
{
    public static Result<Money> CalculateBasePrice(
        Money nightlyRate,
        StayPeriod stayPeriod)
    {
        ArgumentNullException.ThrowIfNull(nightlyRate);
        ArgumentNullException.ThrowIfNull(stayPeriod);

        return nightlyRate.Multiply(stayPeriod.NumberOfNights);
    }
}
