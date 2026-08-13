using Bookify.Services.Booking.Domain.Shared;

namespace Bookify.Services.Booking.Domain.Bookings.Errors;

public static class PricingSeasonErrors
{
    public static readonly Error InvalidDateRange =
        Error.Validation(
            "PricingSeason.InvalidDateRange",
            "The season end date must be after the start date.");

    public static readonly Error InvalidPriority =
        Error.Validation(
            "PricingSeason.InvalidPriority",
            "The season priority cannot be negative.");

    public static Error AmbiguousPriority(
        DateOnly night,
        int priority) =>
        Error.Conflict(
            "PricingSeason.AmbiguousPriority",
            $"Multiple pricing seasons with priority '{priority}' apply to night '{night:yyyy-MM-dd}'.");
}
