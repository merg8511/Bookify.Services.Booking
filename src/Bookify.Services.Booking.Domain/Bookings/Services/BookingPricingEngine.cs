using Bookify.Services.Booking.Domain.Bookings.ValueObjects;
using Bookify.Services.Booking.Domain.Properties;
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

    public static Result<Money> CalculateExtraGuestPrice(
        Money extraGuestNightlyRate,
        RentableUnit rentableUnit,
        GuestCount guestCount,
        StayPeriod stayPeriod)
    {
        ArgumentNullException.ThrowIfNull(extraGuestNightlyRate);
        ArgumentNullException.ThrowIfNull(rentableUnit);
        ArgumentNullException.ThrowIfNull(guestCount);
        ArgumentNullException.ThrowIfNull(stayPeriod);

        int extraGuestCount =
            Math.Max(
                0,
                guestCount.Value - rentableUnit.MaxBaseGuests);

        int extraGuestNights = extraGuestCount * stayPeriod.NumberOfNights;

        return extraGuestNightlyRate.Multiply(extraGuestNights);
    }
}
