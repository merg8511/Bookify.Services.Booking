using Bookify.Services.Booking.Domain.Bookings.Errors;
using Bookify.Services.Booking.Domain.Shared;
using Bookify.Services.Booking.Domain.Shared.ValueObjects;

namespace Bookify.Services.Booking.Domain.Bookings.Pricing;

public sealed record PricingSeason
{
    private PricingSeason(
        DateOnly startDate,
        DateOnly endDate,
        Money nightlyRate,
        int priority)
    {
        StartDate = startDate;
        EndDate = endDate;
        NightlyRate = nightlyRate;
        Priority = priority;
    }

    public DateOnly StartDate { get; }
    public DateOnly EndDate { get; }
    public Money NightlyRate { get; }
    public int Priority { get; }

    public static Result<PricingSeason> Create(
        DateOnly startDate,
        DateOnly endDate,
        Money nightlyRate,
        int priority)
    {
        ArgumentNullException.ThrowIfNull(nightlyRate);

        if (endDate <= startDate)
        {
            return Result<PricingSeason>
                .Failure(PricingSeasonErrors.InvalidDateRange);
        }

        if (priority < 0)
        {
            return Result<PricingSeason>
                .Failure(PricingSeasonErrors.InvalidPriority);
        }

        return Result<PricingSeason>.Success(
            new PricingSeason(
                startDate,
                endDate,
                nightlyRate,
                priority));
    }

    public bool ContainsNight(DateOnly night)
    {
        return night >= StartDate && night < EndDate;
    }
}
