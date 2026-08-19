using Bookify.Services.Booking.Domain.Bookings.Pricing;
using Bookify.Services.Booking.Domain.Shared.ValueObjects;

namespace Bookify.Services.Booking.Domain.Tests.Bookings.Pricing;

public sealed class PriceSnapshotTests
{
    [Fact]
    public void Create_WithPriceBreakdown_ShouldCaptureAllPriceComponents()
    {
        // ARRANGE
        PriceBreakdown priceBreakdown =
            PriceBreakdown.Create(
                Money.Create(
                    700m,
                    "USD")
                .Value,
                Money.Create(
                    200m,
                    "USD")
                .Value)
            .Value;

        // ACT
        PriceSnapshot snapshot =
            PriceSnapshot.Create(
                priceBreakdown);

        // ASSERT
        Assert.Equal(
            700m,
            snapshot.AccommodationPrice.Amount);

        Assert.Equal(
            200m,
            snapshot.ExtraGuestPrice.Amount);

        Assert.Equal(
            900m,
            snapshot.TotalPrice.Amount);

        Assert.Equal(
            "USD",
            snapshot.TotalPrice.Currency);
    }

    [Fact]
    public void Create_WhenNewBreakdownIsCalculated_ShouldPreserveOriginalSnapshot()
    {
        // ARRANGE
        PriceBreakdown originalBreakdown =
            PriceBreakdown.Create(
                Money.Create(
                    400m,
                    "USD")
                .Value,
                Money.Create(
                    50m,
                    "USD")
                .Value)
            .Value;

        PriceSnapshot snapshot =
            PriceSnapshot.Create(
                originalBreakdown);

        // ACT
        PriceBreakdown recalculatedBreakdown =
            PriceBreakdown.Create(
                Money.Create(
                    520m,
                    "USD")
                .Value,
                Money.Create(
                    50m,
                    "USD")
                .Value)
            .Value;

        // ASSERT
        Assert.Equal(
            450m,
            snapshot.TotalPrice.Amount);

        Assert.Equal(
            570m,
            recalculatedBreakdown.TotalPrice.Amount);

        Assert.NotEqual(
            snapshot.TotalPrice,
            recalculatedBreakdown.TotalPrice);
    }

    [Fact]
    public void Create_WithEquivalentBreakdowns_ShouldProduceEqualSnapshots()
    {
        // ARRANGE
        PriceBreakdown firstBreakdown =
            PriceBreakdown.Create(
                Money.Create(
                    400m,
                    "USD")
                .Value,
                Money.Create(
                    50m,
                    "USD")
                .Value)
            .Value;

        PriceBreakdown secondBreakdown =
            PriceBreakdown.Create(
                Money.Create(
                    400m,
                    "USD")
                .Value,
                Money.Create(
                    50m,
                    "USD")
                .Value)
            .Value;

        // ACT
        PriceSnapshot firstSnapshot =
            PriceSnapshot.Create(
                firstBreakdown);

        PriceSnapshot secondSnapshot =
            PriceSnapshot.Create(
                secondBreakdown);

        // ASSERT
        Assert.Equal(
            firstSnapshot,
            secondSnapshot);
    }

    [Fact]
    public void Create_WithNullBreakdown_ShouldThrow()
    {
        // ACT
        void Action()
        {
            PriceSnapshot.Create(
                null!);
        }

        // ASSERT
        Assert.Throws<ArgumentNullException>(
            Action);
    }
}
