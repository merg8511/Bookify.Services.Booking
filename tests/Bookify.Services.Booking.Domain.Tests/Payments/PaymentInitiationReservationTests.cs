
using Bookify.Services.Booking.Domain.Payments;
using Bookify.Services.Booking.Domain.Shared.ValueObjects;

namespace Bookify.Services.Booking.Domain.Tests.Payments;

public sealed class PaymentInitiationReservationTests
{
    private static readonly DateTimeOffset UtcNow =
        new(2026, 10, 15, 18, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ReserveInitiation_ShouldPreserveOperationIdentity()
    {
        Payment payment = CreatePayment();

        payment.ReserveInitiation(
            "operation-001",
            UtcNow);

        Assert.Equal(
            "operation-001",
            payment.PendingInitiationKey);

        Assert.Equal(
            UtcNow,
            payment.PendingInitiationCreatedAtUtc);

        Assert.Empty(payment.Attempts);
    }

    [Fact]
    public void ReserveInitiation_WhenAlreadyReserved_ShouldThrow()
    {
        Payment payment = CreatePayment();

        payment.ReserveInitiation("operation-001", UtcNow);

        Assert.Throws<InvalidOperationException>(
            () => payment.ReserveInitiation(
                "operation-002",
                UtcNow.AddMinutes(1)));

        Assert.Equal(
            "operation-001",
            payment.PendingInitiationKey);
    }

    [Fact]
    public void CompleteInitiation_WithoutPaymentAttempt_ShouldThrow()
    {
        Payment payment = CreatePayment();

        payment.ReserveInitiation("operation-001", UtcNow);

        Assert.Throws<InvalidOperationException>(
            () => payment.CompleteInitiation("operation-001"));

        Assert.Equal(
            "operation-001",
            payment.PendingInitiationKey);
    }

    [Fact]
    public void CompleteInitiation_WithDifferentKey_ShouldThrow()
    {
        Payment payment = CreatePayment();

        payment.ReserveInitiation("operation-001", UtcNow);

        Assert.Throws<InvalidOperationException>(
            () => payment.CompleteInitiation("operation-002"));

        Assert.Equal(
            "operation-001",
            payment.PendingInitiationKey);
    }

    [Fact]
    public void CompleteInitiation_WithRecordedAttempt_ShouldClearReservation()
    {
        Payment payment = CreatePayment();

        payment.ReserveInitiation("operation-001", UtcNow);

        var attemptResult = payment.AddAttempt(
            "operation-001",
            "pi_original",
            UtcNow.AddSeconds(1));

        Assert.True(attemptResult.IsSuccess);

        payment.CompleteInitiation("operation-001");

        Assert.Null(payment.PendingInitiationKey);
        Assert.Null(payment.PendingInitiationCreatedAtUtc);

        Assert.Single(payment.Attempts);
    }

    private static Payment CreatePayment()
    {
        var moneyResult = Money.Create(100m, "USD");

        Assert.True(moneyResult.IsSuccess);

        var paymentResult = Payment.Create(
            Guid.NewGuid(),
            moneyResult.Value,
            UtcNow.AddMinutes(-1));

        Assert.True(paymentResult.IsSuccess);

        return paymentResult.Value;
    }
}
