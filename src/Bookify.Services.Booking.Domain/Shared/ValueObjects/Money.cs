using Bookify.Services.Booking.Domain.Shared.Errors;

namespace Bookify.Services.Booking.Domain.Shared.ValueObjects;

public sealed record Money
{
    private Money(decimal amount, string currency)
    {
        Amount = amount;
        Currency = currency;
    }

    public decimal Amount { get; }
    public string Currency { get; }

    public static Result<Money> Create(
        decimal amount,
        string currency)
    {
        if (amount < 0)
        {
            return Result<Money>.Failure(
                MoneyErrors.NegativeAmount);
        }

        string normalizedCurrency =
            currency
            .Trim()
            .ToUpperInvariant()
            ?? string.Empty;

        bool hasValidCurrencyFormat =
            normalizedCurrency.Length == 3 &&
            normalizedCurrency.All(
                character =>
                    character is >= 'A' and <= 'Z');

        if (!hasValidCurrencyFormat)
        {
            return Result<Money>.Failure(
                MoneyErrors.InvalidCurrency);
        }

        return Result<Money>.Success(
            new Money(
                amount,
                normalizedCurrency));
    }

    public Result<Money> Add(Money other)
    {
        ArgumentNullException.ThrowIfNull(other);

        if (!HasSameCurrency(other))
        {
            return Result<Money>.Failure(
                MoneyErrors.CurrencyMismatch(Currency, other.Currency));
        }

        return Result<Money>.Success(
            new Money(
                Amount + other.Amount,
                Currency));
    }

    public Result<Money> Multiply(int multiplier)
    {
        if (multiplier < 0)
        {
            return Result<Money>.Failure(
                MoneyErrors.InvalidMultiplier);
        }

        return Result<Money>.Success(
            new Money(
                Amount * multiplier,
                Currency));
    }

    public bool HasSameCurrency(Money other)
    {
        ArgumentNullException.ThrowIfNull(other);

        return string.Equals(
            Currency,
            other.Currency,
            StringComparison.Ordinal);
    }
}
