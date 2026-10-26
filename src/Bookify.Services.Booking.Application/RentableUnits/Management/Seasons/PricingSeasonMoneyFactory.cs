using Bookify.Services.Booking.Domain.Shared;
using Bookify.Services.Booking.Domain.Shared.ValueObjects;

namespace Bookify.Services.Booking.Application.RentableUnits.Management.Seasons;

internal static class PricingSeasonMoneyFactory
{
    private const decimal MaxSupportedAmount = 999999999999999.999m;

    public static Result<Money> Create(decimal amount, string currency)
    {
        Result<Money> moneyResult = Money.Create(amount, currency);

        if (moneyResult.IsFailure)
        {
            return moneyResult;
        }

        if (moneyResult.Value.Amount > MaxSupportedAmount)
        {
            return Result<Money>.Failure(PricingSeasonManagementErrors.AmountOutOfRange);
        }

        return moneyResult;
    }
}
