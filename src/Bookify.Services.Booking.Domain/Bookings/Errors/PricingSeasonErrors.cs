using Bookify.Services.Booking.Domain.Shared;

namespace Bookify.Services.Booking.Domain.Bookings.Errors;

public static class PricingSeasonErrors
{
    public static readonly Error InvalidDateRange = Error.Validation(
        "PricingSeason.InvalidDateRange",
        "The season end date must be after the start date.");

    public static readonly Error InvalidPriority = Error.Validation(
        "PricingSeason.InvalidPriority",
        "The season priority cannot be negative.");

    public static Error AmbiguousPriority(DateOnly night, int priority) => Error.Conflict(
        "PricingSeason.AmbiguousPriority",
        $"Multiple pricing seasons with priority '{priority}' apply to night '{night:yyyy-MM-dd}'.");

    public static Error NotFound(int seasonId) => Error.NotFound(
        "PricingSeason.NotFound",
        $"The pricing season with ID '{seasonId}' was not found.");

    public static readonly Error PricingNotConfigured = Error.Conflict(
        "PricingSeason.PricingNotConfigured",
        "Base pricing must be configured before managing pricing seasons.");

    public static readonly Error CurrencyMismatch = Error.Conflict(
        "PricingSeason.CurrentMismatch",
        "The pricing season currency must match the rentable unit base pricing currency.");
}
