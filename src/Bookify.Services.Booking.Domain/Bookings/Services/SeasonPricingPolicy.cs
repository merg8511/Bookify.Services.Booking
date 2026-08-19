using Bookify.Services.Booking.Domain.Bookings.Errors;
using Bookify.Services.Booking.Domain.Bookings.Pricing;
using Bookify.Services.Booking.Domain.Shared;
using Bookify.Services.Booking.Domain.Shared.ValueObjects;

namespace Bookify.Services.Booking.Domain.Bookings.Services;

public static class SeasonPricingPolicy
{
    public static Result<Money> ResolveNightlyRate(
        DateOnly night,
        Money fallbackRate,
        IReadOnlyCollection<PricingSeason> seasons)
    {
        ArgumentNullException.ThrowIfNull(fallbackRate);
        ArgumentNullException.ThrowIfNull(seasons);

        PricingSeason? selectedSeason = null;
        bool hasAmbiguousPriority = false;

        foreach (PricingSeason season in seasons)
        {
            if (!season.ContainsNight(night))
            {
                continue;
            }

            if (selectedSeason is null
                || season.Priority > selectedSeason.Priority)
            {
                selectedSeason = season;
                hasAmbiguousPriority = false;

                continue;
            }

            if (season.Priority == selectedSeason.Priority)
            {
                hasAmbiguousPriority = true;
            }
        }

        if (selectedSeason is null)
        {
            return Result<Money>.Success(fallbackRate);
        }

        if (hasAmbiguousPriority)
        {
            return Result<Money>.Failure(
                 PricingSeasonErrors.AmbiguousPriority(
                     night,
                     selectedSeason.Priority));
        }

        return Result<Money>.Success(
            selectedSeason.NightlyRate);
    }
}
