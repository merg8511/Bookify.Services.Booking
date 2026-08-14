using Bookify.Services.Booking.Domain.Shared;
using Bookify.Services.Booking.Domain.Shared.Errors;
using Bookify.Services.Booking.Domain.Shared.ValueObjects;

namespace Bookify.Services.Booking.Domain.Properties.Pricing;

public sealed record RentableUnitPricing
{
    private RentableUnitPricing() { }
    private RentableUnitPricing(
        Money regularNightlyRate,
        Money weekendNightlyRate,
        Money extraGuestNightRate)
    {
        RegularNightlyRate = regularNightlyRate;
        WeekendNightlyRate = weekendNightlyRate;
        ExtraGuestNightlyRate = extraGuestNightRate;
    }

    public Money RegularNightlyRate { get; private set; } = null!;
    public Money WeekendNightlyRate { get; private set; } = null!;
    public Money ExtraGuestNightlyRate { get; private set; } = null!;

    public static Result<RentableUnitPricing> Create(
        Money regularNightlyRate,
        Money weekendNightlyRate,
        Money extraGuestNightlyRate)
    {
        ArgumentNullException.ThrowIfNull(regularNightlyRate);
        ArgumentNullException.ThrowIfNull(weekendNightlyRate);
        ArgumentNullException.ThrowIfNull(extraGuestNightlyRate);

        if (!regularNightlyRate.HasSameCurrency(weekendNightlyRate))
        {
            return Result<RentableUnitPricing>.Failure(
                MoneyErrors.CurrencyMismatch(
                    regularNightlyRate.Currency,
                    weekendNightlyRate.Currency));
        }

        if (!regularNightlyRate.HasSameCurrency(extraGuestNightlyRate))
        {
            return Result<RentableUnitPricing>.Failure(
                MoneyErrors.CurrencyMismatch(
                    regularNightlyRate.Currency,
                    extraGuestNightlyRate.Currency));
        }

        return Result<RentableUnitPricing>.Success(
            new RentableUnitPricing(
                regularNightlyRate,
                weekendNightlyRate,
                extraGuestNightlyRate));
    }

}
