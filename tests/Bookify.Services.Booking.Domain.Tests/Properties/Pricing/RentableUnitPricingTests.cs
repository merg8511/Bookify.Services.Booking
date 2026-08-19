using Bookify.Services.Booking.Domain.Properties.Pricing;
using Bookify.Services.Booking.Domain.Shared.Errors;
using Bookify.Services.Booking.Domain.Shared.ValueObjects;

namespace Bookify.Services.Booking.Domain.Tests.Properties.Pricing;

public sealed class RentableUnitPricingTests
{
    [Fact]
    public void Create_WithSameCurrency_ShouldReturnPricing()
    {
        // ARRANGE
        Money regularNightlyRate =
            Money.Create(
                100m,
                "USD")
            .Value;

        Money weekendNightlyRate =
            Money.Create(
                140m,
                "USD")
            .Value;

        Money extraGuestNightlyRate =
            Money.Create(
                25m,
                "USD")
            .Value;

        // ACT
        var result =
            RentableUnitPricing.Create(
                regularNightlyRate,
                weekendNightlyRate,
                extraGuestNightlyRate);

        // ASSERT
        Assert.True(result.IsSuccess);

        Assert.Equal(
            regularNightlyRate,
            result.Value.RegularNightlyRate);

        Assert.Equal(
            weekendNightlyRate,
            result.Value.WeekendNightlyRate);

        Assert.Equal(
            extraGuestNightlyRate,
            result.Value.ExtraGuestNightlyRate);
    }

    [Fact]
    public void Create_WithDifferentWeekendCurrency_ShouldReturnFailure()
    {
        // ARRANGE
        Money regularNightlyRate =
            Money.Create(
                100m,
                "USD")
            .Value;

        Money weekendNightlyRate =
            Money.Create(
                140m,
                "EUR")
            .Value;

        Money extraGuestNightlyRate =
            Money.Create(
                25m,
                "USD")
            .Value;

        // ACT
        var result =
            RentableUnitPricing.Create(
                regularNightlyRate,
                weekendNightlyRate,
                extraGuestNightlyRate);

        // ASSERT
        Assert.True(result.IsFailure);

        Assert.Equal(
            MoneyErrors.CurrencyMismatch(
                "USD",
                "EUR"),
            result.Error);
    }

    [Fact]
    public void Create_WithDifferentExtraGuestCurrency_ShouldReturnFailure()
    {
        // ARRANGE
        Money regularNightlyRate =
            Money.Create(
                100m,
                "USD")
            .Value;

        Money weekendNightlyRate =
            Money.Create(
                140m,
                "USD")
            .Value;

        Money extraGuestNightlyRate =
            Money.Create(
                25m,
                "EUR")
            .Value;

        // ACT
        var result =
            RentableUnitPricing.Create(
                regularNightlyRate,
                weekendNightlyRate,
                extraGuestNightlyRate);

        // ASSERT
        Assert.True(result.IsFailure);

        Assert.Equal(
            MoneyErrors.CurrencyMismatch(
                "USD",
                "EUR"),
            result.Error);
    }

    [Fact]
    public void Create_WithZeroRates_ShouldReturnSuccess()
    {
        // ARRANGE
        Money zero =
            Money.Create(
                0m,
                "USD")
            .Value;

        // ACT
        var result =
            RentableUnitPricing.Create(
                zero,
                zero,
                zero);

        // ASSERT
        Assert.True(result.IsSuccess);
    }
}
