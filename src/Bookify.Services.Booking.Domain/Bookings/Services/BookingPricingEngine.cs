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

    public static Result<Money> CalculateAccommodationPrice(
        Money regularNightlyRate,
        Money weekendNightlyRate,
        StayPeriod stayPeriod)
    {
        ArgumentNullException.ThrowIfNull(regularNightlyRate);
        ArgumentNullException.ThrowIfNull(weekendNightlyRate);
        ArgumentNullException.ThrowIfNull(stayPeriod);

        int regularNightCount = 0;
        int weekendNightCount = 0;

        for (
            DateOnly night = stayPeriod.CheckInDate;
            night < stayPeriod.CheckOutDate;
            night = night.AddDays(1))
        {
            if (WeekendPricingPolicy.IsWeekendNight(night))
            {
                weekendNightCount++;
            }
            else
            {
                regularNightCount++;
            }
        }

        Result<Money> regularPriceResult = regularNightlyRate.Multiply(regularNightCount);

        if (regularPriceResult.IsFailure)
        {
            return Result<Money>.Failure(regularPriceResult.Error);
        }

        Result<Money> weekendPriceResult = weekendNightlyRate.Multiply(weekendNightCount);

        if (weekendPriceResult.IsFailure)
        {
            return Result<Money>.Failure(weekendPriceResult.Error);
        }

        return regularPriceResult.Value.Add(weekendPriceResult.Value);
    }
}
