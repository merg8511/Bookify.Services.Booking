using Bookify.Services.Booking.Domain.Bookings.Pricing;
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
        ArgumentNullException.ThrowIfNull(rentableUnit);

        return CalculateExtraGuestPrice(
            extraGuestNightlyRate,
            rentableUnit.MaxBaseGuests,
            guestCount,
            stayPeriod);
    }

    public static Result<Money> CalculateExtraGuestPrice(
        Money extraGuestNightlyRate,
        int maxBaseGuests,
        GuestCount guestCount,
        StayPeriod stayPeriod)
    {
        ArgumentNullException.ThrowIfNull(extraGuestNightlyRate);
        ArgumentNullException.ThrowIfNull(guestCount);
        ArgumentNullException.ThrowIfNull(stayPeriod);

        if (maxBaseGuests <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxBaseGuests),
                "Maximum base guests must be greater than zero.");
        }

        int extraGuestCount = Math.Max(0, guestCount.Value - maxBaseGuests);
        int extraGuestNights = extraGuestCount * stayPeriod.NumberOfNights;

        return extraGuestNightlyRate.Multiply(extraGuestNights);
    }

    public static Result<Money> CalculateAccommodationPrice(
        Money regularNightlyRate,
        Money weekendNightlyRate,
        StayPeriod stayPeriod)
    {
        return CalculateAccommodationPrice(
            regularNightlyRate,
            weekendNightlyRate,
            stayPeriod,
            Array.Empty<PricingSeason>());
    }

    public static Result<Money> CalculateAccommodationPrice(
        Money regularNightlyRate,
        Money weekendNightlyRate,
        StayPeriod stayPeriod,
        IReadOnlyCollection<PricingSeason> seasons)
    {
        ArgumentNullException.ThrowIfNull(regularNightlyRate);
        ArgumentNullException.ThrowIfNull(weekendNightlyRate);
        ArgumentNullException.ThrowIfNull(stayPeriod);
        ArgumentNullException.ThrowIfNull(seasons);

        Money total = regularNightlyRate.Multiply(0).Value;

        for (DateOnly night = stayPeriod.CheckInDate;
            night < stayPeriod.CheckOutDate;
            night = night.AddDays(1))
        {
            Money fallbackRate = WeekendPricingPolicy.IsWeekendNight(night)
                ? weekendNightlyRate
                : regularNightlyRate;

            Result<Money> nightlyRateResult = SeasonPricingPolicy.ResolveNightlyRate(
                night,
                fallbackRate,
                seasons);

            if (nightlyRateResult.IsFailure)
            {
                return Result<Money>.Failure(nightlyRateResult.Error);
            }
            Result<Money> totalResult = total.Add(nightlyRateResult.Value);

            if (totalResult.IsFailure)
            {
                return Result<Money>.Failure(totalResult.Error);
            }

            total = totalResult.Value;
        }

        return Result<Money>.Success(total);
    }

    public static Result<PriceBreakdown> CalculatePrice(
        Money regularNightlyRate,
        Money weekendNightlyRate,
        Money extraGuestNightlyRate,
        RentableUnit rentableUnit,
        GuestCount guestCount,
        StayPeriod stayPeriod)
    {
        ArgumentNullException.ThrowIfNull(
            rentableUnit);

        return CalculatePrice(
            regularNightlyRate,
            weekendNightlyRate,
            extraGuestNightlyRate,
            rentableUnit.MaxBaseGuests,
            guestCount,
            stayPeriod,
            Array.Empty<PricingSeason>());
    }

    public static Result<PriceBreakdown> CalculatePrice(
        Money regularNightlyRate,
        Money weekendNightlyRate,
        Money extraGuestNightlyRate,
        int maxBaseGuests,
        GuestCount guestCount,
        StayPeriod stayPeriod)
    {
        return CalculatePrice(
            regularNightlyRate,
            weekendNightlyRate,
            extraGuestNightlyRate,
            maxBaseGuests,
            guestCount,
            stayPeriod,
            Array.Empty<PricingSeason>());
    }

    public static Result<PriceBreakdown> CalculatePrice(
        Money regularNightlyRate,
        Money weekendNightlyRate,
        Money extraGuestNightlyRate,
        RentableUnit rentableUnit,
        GuestCount guestCount,
        StayPeriod stayPeriod,
        IReadOnlyCollection<PricingSeason> seasons)
    {
        ArgumentNullException.ThrowIfNull(rentableUnit);

        return CalculatePrice(
            regularNightlyRate,
            weekendNightlyRate,
            extraGuestNightlyRate,
            rentableUnit.MaxBaseGuests,
            guestCount,
            stayPeriod,
            seasons);
    }

    public static Result<PriceBreakdown> CalculatePrice(
        Money regularNightlyRate,
        Money weekendNightlyRate,
        Money extraGuestNightlyRate,
        int maxBaseGuests,
        GuestCount guestCount,
        StayPeriod stayPeriod,
        IReadOnlyCollection<PricingSeason> seasons)
    {
        ArgumentNullException.ThrowIfNull(regularNightlyRate);
        ArgumentNullException.ThrowIfNull(weekendNightlyRate);
        ArgumentNullException.ThrowIfNull(extraGuestNightlyRate);
        ArgumentNullException.ThrowIfNull(guestCount);
        ArgumentNullException.ThrowIfNull(stayPeriod);
        ArgumentNullException.ThrowIfNull(seasons);

        if (maxBaseGuests <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxBaseGuests),
                "Maximum base guests must be greater than zero.");
        }

        Result<Money> accommodationPriceResult = CalculateAccommodationPrice(
            regularNightlyRate,
            weekendNightlyRate,
            stayPeriod,
            seasons);

        if (accommodationPriceResult.IsFailure)
        {
            return Result<PriceBreakdown>.Failure(accommodationPriceResult.Error);
        }

        Result<Money> extraGuestPriceResult = CalculateExtraGuestPrice(
            extraGuestNightlyRate,
            maxBaseGuests,
            guestCount,
            stayPeriod);

        if (extraGuestPriceResult.IsFailure)
        {
            return Result<PriceBreakdown>.Failure(extraGuestPriceResult.Error);
        }

        return PriceBreakdown.Create(accommodationPriceResult.Value, extraGuestPriceResult.Value);
    }
}
