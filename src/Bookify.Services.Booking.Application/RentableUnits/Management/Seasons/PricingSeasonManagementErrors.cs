using Bookify.Services.Booking.Domain.Shared;

namespace Bookify.Services.Booking.Application.RentableUnits.Management.Seasons;

public static class PricingSeasonManagementErrors
{
    public static readonly Error InvalidSeasonId = Error.Validation(
        "PricingSeason.InvalidId",
        "The pricing season identifier must be greater than zero.");

    public static readonly Error AmountOutOfRange = Error.Validation(
        "PricingSeason.AmountOutOfRange",
        "The nightly rate exceeds the supported database precision.");
}
