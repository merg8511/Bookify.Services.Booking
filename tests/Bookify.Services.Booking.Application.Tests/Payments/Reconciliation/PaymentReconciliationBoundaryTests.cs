
using Bookify.Services.Booking.Application.Abstractions.Payments;
using Bookify.Services.Booking.Application.Payments.Reconciliation;
using Bookify.Services.Booking.Application.Tests.Infrastructure;
using Bookify.Services.Booking.Domain.Bookings;
using Bookify.Services.Booking.Domain.Payments;
using Bookify.Services.Booking.Domain.Shared.ValueObjects;

using DomainBooking =
    Bookify.Services.Booking.Domain.Bookings.Booking;

namespace Bookify.Services.Booking.Application.Tests.Payments.Reconciliation;

public sealed class PaymentReconciliationBoundaryTests
{
    private static readonly DateTimeOffset ObservedAtUtc =
        new(2026, 10, 28, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Reconcile_WithPaymentFromAnotherBooking_ShouldRejectWithoutMutation()
    {
        // ARRANGE
        DomainBooking booking =
            BookingTestFactory.CreatePendingPaymentBooking();

        Payment payment = CreatePayment(Guid.NewGuid());

        PaymentAttempt attempt = CreateAttempt(payment);

        // ACT
        void Action() => PaymentReconciler.Reconcile(
            payment,
            attempt,
            booking,
            PaymentGatewayStatus.Succeeded,
            ObservedAtUtc.AddMinutes(2));

        // ASSERT
        Assert.Throws<InvalidOperationException>(Action);

        Assert.Equal(BookingStatus.PendingPayment, booking.Status);
        Assert.Equal(PaymentStatus.Pending, payment.Status);
        Assert.Equal(PaymentAttemptStatus.Pending, attempt.Status);

        Assert.Null(booking.PaidAtUtc);
        Assert.Null(payment.CompletedAtUtc);
        Assert.Null(attempt.CompletedAtUtc);
    }

    [Fact]
    public void Reconcile_WithAttemptFromAnotherPayment_ShouldRejectWithoutMutation()
    {
        // ARRANGE
        DomainBooking booking =
            BookingTestFactory.CreatePendingPaymentBooking();

        Payment payment = CreatePayment(booking.Id);
        Payment otherPayment = CreatePayment(booking.Id);

        PaymentAttempt foreignAttempt =
            CreateAttempt(otherPayment);

        // ACT
        void Action() => PaymentReconciler.Reconcile(
            payment,
            foreignAttempt,
            booking,
            PaymentGatewayStatus.Succeeded,
            ObservedAtUtc.AddMinutes(2));

        // ASSERT
        Assert.Throws<InvalidOperationException>(Action);

        Assert.Equal(BookingStatus.PendingPayment, booking.Status);
        Assert.Equal(PaymentStatus.Pending, payment.Status);
        Assert.Equal(PaymentStatus.Pending, otherPayment.Status);

        Assert.Empty(payment.Attempts);
        Assert.Single(otherPayment.Attempts);

        Assert.Equal(
            PaymentAttemptStatus.Pending,
            foreignAttempt.Status);

        Assert.Null(booking.PaidAtUtc);
        Assert.Null(payment.CompletedAtUtc);
        Assert.Null(otherPayment.CompletedAtUtc);
    }

    [Fact]
    public void Reconcile_WithUnsupportedGatewayStatus_ShouldRejectWithoutMutation()
    {
        // ARRANGE
        DomainBooking booking =
            BookingTestFactory.CreatePendingPaymentBooking();

        Payment payment = CreatePayment(booking.Id);
        PaymentAttempt attempt = CreateAttempt(payment);

        PaymentGatewayStatus unsupportedStatus =
            (PaymentGatewayStatus)999;

        // ACT
        void Action() => PaymentReconciler.Reconcile(
            payment,
            attempt,
            booking,
            unsupportedStatus,
            ObservedAtUtc.AddMinutes(2));

        // ASSERT
        Assert.Throws<InvalidOperationException>(Action);

        Assert.Equal(BookingStatus.PendingPayment, booking.Status);
        Assert.Equal(PaymentStatus.Pending, payment.Status);
        Assert.Equal(PaymentAttemptStatus.Pending, attempt.Status);

        Assert.Null(booking.PaidAtUtc);
        Assert.Null(payment.CompletedAtUtc);
        Assert.Null(attempt.CompletedAtUtc);
    }

    private static Payment CreatePayment(Guid bookingId)
    {
        return Payment.Create(
            bookingId,
            Money.Create(150m, "USD").Value,
            ObservedAtUtc).Value;
    }

    private static PaymentAttempt CreateAttempt(Payment payment)
    {
        return payment.AddAttempt(
            $"operation-{Guid.NewGuid():N}",
            $"pi_{Guid.NewGuid():N}",
            ObservedAtUtc.AddMinutes(1)).Value;
    }
}
