using Bookify.Services.Booking.Domain.Shared;

namespace Bookify.Services.Booking.Application.RentableUnits.Management.Pricing;

public static class RentableUnitPricingManagementErrors
{
    public static readonly Error AmountOutOfRange = Error.Validation(
        "Pricing.AmountOutOfRange",
        "The pricing amount exceeds the supported database precision.");

    public static readonly Error SeasonCurrencyMismatch = Error.Conflict(
        "Pricing.SeasonCurrencyMismatch",
        "The requested pricing currency conflicts with existing pricing seasons.");
}
