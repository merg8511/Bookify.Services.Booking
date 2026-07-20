using Bookify.Services.Booking.Domain.Shared.Errors;
using Bookify.Services.Booking.Domain.Shared.ValueObjects;

namespace Bookify.Services.Booking.Domain.Tests.Shared.ValueObjects
{
    public sealed class MoneyTests
    {
        [Fact]
        public void Create_WithValidDate_ShouldReturnMoney()
        {
            // ACT
            var result = Money.Create(
                250.50m,
                "USD");

            // ASSERT
            Assert.True(result.IsSuccess);
            Assert.Equal(250.50m, result.Value.Amount);
            Assert.Equal("USD", result.Value.Currency);
        }

        [Fact]
        public void Create_ShouldNormalizeCurrency()
        {
            // ACT
            var result = Money.Create(
                250m,
                "  usd  ");

            // ASSERT
            Assert.True(result.IsSuccess);
            Assert.Equal("USD", result.Value.Currency);
        }

        [Fact]
        public void Create_WithNegativeAmount_ShouldReturnFailure()
        {
            // ACT
            var result = Money.Create(
                -1m,
                "USD");

            // ASSERT
            Assert.True(result.IsFailure);

            Assert.Equal(
                MoneyErrors.NegativeAmount,
                result.Error);
        }

        [Theory]
        [InlineData("")]
        [InlineData("  ")]
        [InlineData("US")]
        [InlineData("USDD")]
        [InlineData("12D")]
        public void Create_WithInvalidCurrency_ShouldReturnFailure(string currency)
        {
            // ACT
            var result = Money.Create(
                100m,
                currency);

            // ASSERT
            Assert.True(result.IsFailure);

            Assert.Equal(
                MoneyErrors.InvalidCurrency,
                result.Error);
        }

        [Fact]
        public void Money_WithSameValues_ShouldBeEqual()
        {
            // ARRANGE
            var firstMoney = Money.Create(
                100m,
                "USD")
                .Value;

            var secondMoney = Money.Create(
               100m,
               "USD")
               .Value;

            // ASSERT
            Assert.Equal(
                firstMoney,
                secondMoney);
        }

        [Fact]
        public void Add_WithSameCurrency_ShouldReturnCombineAmount()
        {
            // ARRANGE
            var firstMoney = Money.Create(
                100m,
                "USD")
                .Value;

            var secondMoney = Money.Create(
                50m,
                "USD")
                .Value;

            // ACT
            var result = firstMoney.Add(
                secondMoney);

            // ASSERT
            Assert.True(result.IsSuccess);
            Assert.Equal(150m, result.Value.Amount);
            Assert.Equal("USD", result.Value.Currency);
        }

        [Fact]
        public void Add_WithDifferentCurrencies_ShouldReturnFailure()
        {
            // ARRANGE
            var dollars = Money.Create(
                100m,
                "USD")
                .Value;

            var euros = Money.Create(
                50m,
                "EUR")
                .Value;

            // ACT
            var result = dollars.Add(euros);

            // ASSERT
            Assert.True(result.IsFailure);
            Assert.Equal(
                "Money.CurrencyMismatch",
                result.Error.Code);
        }

        [Theory]
        [InlineData(0, 0)]
        [InlineData(1, 250)]
        [InlineData(3, 750)]
        public void Multiply_WithNonNegativeMultiplier_ShouldReturnTotal(
        int multiplier,
        decimal expectedAmount)
        {
            // ARRANGE
            var money = Money.Create(
                250m,
                "USD")
                .Value;

            // ACT
            var result = money.Multiply(multiplier);

            // ASSERT
            Assert.True(result.IsSuccess);
            Assert.Equal(
                expectedAmount,
                result.Value.Amount);

            Assert.Equal(
                "USD",
                result.Value.Currency);
        }

        [Fact]
        public void Multiply_WithNegativeMultiplier_ShouldReturnFailure()
        {
            // ARRANGE
            var money = Money.Create(
                250m,
                "USD")
                .Value;

            // ACT
            var result = money.Multiply(-1);

            // ASSERT
            Assert.True(result.IsFailure);

            Assert.Equal(
                MoneyErrors.InvalidMultiplier,
                result.Error);
        }
    }
}
