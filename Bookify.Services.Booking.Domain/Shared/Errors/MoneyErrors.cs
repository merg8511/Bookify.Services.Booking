namespace Bookify.Services.Booking.Domain.Shared.Errors;

public static class MoneyErrors
{
    public static readonly Error NegativeAmount = Error.Validation(
        "Money.NegativeAmount",
        "The currency amount cannot be negative.");

    public static readonly Error InvalidCurrency = Error.Validation(
        "Money.InvalidCurrency",
        "The currency must contain a valid three-letter code.");

    public static Error CurrencyMismatch(
        string currentCurrency,
        string otherCurrency) =>
            Error.Validation(
                "Money.CurrencyMismatch",
                $"Cannot combine '{currentCurrency}' with '{otherCurrency}'");

    public static readonly Error InvalidMultiplier = Error.Validation(
        "Money.InvalidMultiplier",
        "The money multiplier cannot be negative");
}
