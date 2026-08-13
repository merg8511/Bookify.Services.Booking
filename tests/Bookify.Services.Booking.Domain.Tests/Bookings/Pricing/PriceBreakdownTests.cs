using Bookify.Services.Booking.Domain.Bookings.Pricing;
using Bookify.Services.Booking.Domain.Shared.Errors;
using Bookify.Services.Booking.Domain.Shared.ValueObjects;

namespace Bookify.Services.Booking.Domain.Tests.Bookings.Pricing;

public sealed class PriceBreakdownTests
{
    [Fact]
    public void Create_WithValidComponents_ShouldCalculateTotalPrice()
    {
        // ARRANGE
        Money accommodationPrice =
            Money.Create(
                700m,
                "USD")
            .Value;

        Money extraGuestPrice =
            Money.Create(
                200m,
                "USD")
            .Value;

        // ACT
        var result =
            PriceBreakdown.Create(
                accommodationPrice,
                extraGuestPrice);

        // ASSERT
        Assert.True(result.IsSuccess);

        Assert.Equal(
            accommodationPrice,
            result.Value.AccommodationPrice);

        Assert.Equal(
            extraGuestPrice,
            result.Value.ExtraGuestPrice);

        Assert.Equal(
            900m,
            result.Value.TotalPrice.Amount);

        Assert.Equal(
            "USD",
            result.Value.TotalPrice.Currency);
    }

    [Fact]
    public void Create_WithZeroExtraGuestPrice_ShouldKeepAccommodationAsTotal()
    {
        // ARRANGE
        Money accommodationPrice =
            Money.Create(
                700m,
                "USD")
            .Value;

        Money extraGuestPrice =
            Money.Create(
                0m,
                "USD")
            .Value;

        // ACT
        var result =
            PriceBreakdown.Create(
                accommodationPrice,
                extraGuestPrice);

        // ASSERT
        Assert.True(result.IsSuccess);

        Assert.Equal(
            700m,
            result.Value.TotalPrice.Amount);
    }

    [Fact]
    public void Create_WithDifferentCurrencies_ShouldReturnFailure()
    {
        // ARRANGE
        Money accommodationPrice =
            Money.Create(
                700m,
                "USD")
            .Value;

        Money extraGuestPrice =
            Money.Create(
                200m,
                "EUR")
            .Value;

        // ACT
        var result =
            PriceBreakdown.Create(
                accommodationPrice,
                extraGuestPrice);

        // ASSERT
        Assert.True(result.IsFailure);

        Assert.Equal(
            MoneyErrors.CurrencyMismatch(
                "USD",
                "EUR"),
            result.Error);
    }
}
