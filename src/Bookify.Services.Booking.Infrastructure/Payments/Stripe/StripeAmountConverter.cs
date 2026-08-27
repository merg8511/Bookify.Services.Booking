using Bookify.Services.Booking.Application.Abstractions.Payments;
using Bookify.Services.Booking.Domain.Shared;
using Bookify.Services.Booking.Domain.Shared.ValueObjects;

namespace Bookify.Services.Booking.Infrastructure.Payments.Stripe;

internal static class StripeAmountConverter
{
    private static readonly HashSet<string>
        ZeroDecimalCurrencies = new(StringComparer.Ordinal)
        {
            "BIF",
            "CLP",
            "DJF",
            "GNF",
            "JPY",
            "KMF",
            "KRW",
            "MGA",
            "PYG",
            "RWF",
            "VND",
            "VUV",
            "XAF",
            "XOF",
            "XPF"
        };

    private static readonly HashSet<string> ThreeDecimalCurrencies =
        new(StringComparer.Ordinal)
        {
            "BHD",
            "JOD",
            "KWD",
            "OMR",
            "TND"
        };

    private static readonly HashSet<string> WholeAmountCurrenciesWithTwoDecimalMinorUnit =
        new(StringComparer.Ordinal)
        {
            "ISK",
            "UGX"
        };

    public static Result<long> ToMinorUnits(Money amount)
    {
        ArgumentNullException.ThrowIfNull(amount);

        if (amount.Amount <= 0)
        {
            return Result<long>.Failure(
                PaymentGatewayErrors
                    .AmountMustBePositive);
        }

        if (WholeAmountCurrenciesWithTwoDecimalMinorUnit
            .Contains(amount.Currency) &&
            amount.Amount !=
            decimal.Truncate(amount.Amount))
        {
            return Result<long>.Failure(
                PaymentGatewayErrors
                    .InvalidAmountPrecision(amount.Currency));
        }

        decimal multiplier =
            WholeAmountCurrenciesWithTwoDecimalMinorUnit.Contains(amount.Currency)
                ? 100m
            : ThreeDecimalCurrencies.Contains(amount.Currency)
                ? 1000m
            : ZeroDecimalCurrencies.Contains(amount.Currency)
                ? 1m
            : 100m;

        decimal minorUnits = amount.Amount * multiplier;

        if (minorUnits != decimal.Truncate(minorUnits))
        {
            return Result<long>.Failure(
                PaymentGatewayErrors
                    .InvalidAmountPrecision(amount.Currency));
        }

        if (minorUnits > long.MaxValue)
        {
            return Result<long>.Failure(
                PaymentGatewayErrors
                    .AmountOutOfRange(amount.Currency));
        }

        return Result<long>.Success(
            decimal.ToInt64(minorUnits));
    }
}
