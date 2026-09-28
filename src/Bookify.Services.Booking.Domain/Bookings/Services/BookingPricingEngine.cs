using Bookify.Services.Booking.Domain.Bookings.Pricing;
using Bookify.Services.Booking.Domain.Bookings.ValueObjects;
using Bookify.Services.Booking.Domain.Properties;
using Bookify.Services.Booking.Domain.Properties.Pricing;
using Bookify.Services.Booking.Domain.Shared;
using Bookify.Services.Booking.Domain.Shared.ValueObjects;

namespace Bookify.Services.Booking.Domain.Bookings.Services;

public static class BookingPricingEngine
{
    public static Result<PriceBreakdown> CalculatePrice(
        RentableUnitPricing pricing,
        int maxBaseGuests,
        GuestCount guestCount,
        StayPeriod stayPeriod,
        IReadOnlyCollection<PricingSeason> seasons)
    {
        ArgumentNullException.ThrowIfNull(pricing);
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
            pricing,
            stayPeriod,
            seasons);

        if (accommodationPriceResult.IsFailure)
        {
            return Result<PriceBreakdown>.Failure(accommodationPriceResult.Error);
        }

        Result<Money> extraGuestPriceResult = CalculateExtraGuestPrice(
            pricing,
            maxBaseGuests,
            guestCount,
            stayPeriod);

        if (extraGuestPriceResult.IsFailure)
        {
            return Result<PriceBreakdown>.Failure(extraGuestPriceResult.Error);
        }

        return PriceBreakdown.Create(
            accommodationPriceResult.Value,
            extraGuestPriceResult.Value);
    }

    private static Result<Money> CalculateAccommodationPrice(
        RentableUnitPricing pricing,
        StayPeriod stayPeriod,
        IReadOnlyCollection<PricingSeason> seasons)
    {
        Money total = Money.Create(
            0m,
            pricing.RegularNightlyRate.Currency).Value;

        for (DateOnly night = stayPeriod.CheckInDate;
            night < stayPeriod.CheckOutDate;
            night = night.AddDays(1))
        {
            Money fallbackRate = WeekendPricingPolicy.IsWeekendNight(night)
                ? pricing.WeekendNightlyRate
                : pricing.RegularNightlyRate;

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

    private static Result<Money> CalculateExtraGuestPrice(
        RentableUnitPricing pricing,
        int maxBaseGuests,
        GuestCount guestCount,
        StayPeriod stayPeriod)
    {
        int extraGuestCount = Math.Max(0, guestCount.Value - maxBaseGuests);
        int extraGuestNights = extraGuestCount * stayPeriod.NumberOfNights;

        return pricing.ExtraGuestNightlyRate.Multiply(extraGuestNights);
    }
}
