using Bookify.Services.Booking.Application.Abstractions.Payments;
using Bookify.Services.Booking.Application.Payments.Reconciliation;
using Bookify.Services.Booking.Application.Tests.Infrastructure;
using Bookify.Services.Booking.Domain.Bookings;
using Bookify.Services.Booking.Domain.Payments;
using Bookify.Services.Booking.Domain.Shared;
using DomainBooking = Bookify.Services.Booking.Domain.Bookings.Booking;

namespace Bookify.Services.Booking.Application.Tests.Payments.Reconciliation;

public sealed class PaymentReconcilerTests
{
    private static readonly DateTimeOffset UtcNow = new(2026, 9, 4, 20, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Reconcile_WhenGatewayStatusIsSucceeded_ShouldSucceedPaymentAttemptPaymentAndBooking()
    {
        // Arrange
        DomainBooking booking = BookingTestFactory.CreatePendingPaymentBooking();
        Payment payment = CreatePayment(booking);
        PaymentAttempt attempt = AddPendingAttempt(payment, "operation-succeeded", "external-succeeded");
        DateTimeOffset observedAtUtc = UtcNow.AddMinutes(1);

        // Act
        Result result = PaymentReconciler.Reconcile(
            payment,
            attempt,
            booking,
            PaymentGatewayStatus.Succeeded,
            observedAtUtc);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(PaymentAttemptStatus.Succeeded, attempt.Status);
        Assert.Equal(PaymentStatus.Succeeded, payment.Status);
        Assert.Equal(BookingStatus.Paid, booking.Status);
        Assert.Equal(observedAtUtc, attempt.CompletedAtUtc);
        Assert.Equal(observedAtUtc, booking.PaidAtUtc);
        Assert.Equal(payment.CompletedAtUtc, booking.PaidAtUtc);
    }

    [Fact]
    public void Reconcile_WhenGatewayStatusIsPending_ShouldNotChangeCurrentState()
    {
        // Arrange
        DomainBooking booking = BookingTestFactory.CreatePendingPaymentBooking();
        Payment payment = CreatePayment(booking);
        PaymentAttempt attempt = AddPendingAttempt(payment, "operation-pending", "external-pending");
        DateTimeOffset observedAtUtc = UtcNow.AddMinutes(1);

        // Act
        Result result = PaymentReconciler.Reconcile(
            payment,
            attempt,
            booking,
            PaymentGatewayStatus.Pending,
            observedAtUtc);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(PaymentAttemptStatus.Pending, attempt.Status);
        Assert.Equal(PaymentStatus.Pending, payment.Status);
        Assert.Equal(BookingStatus.PendingPayment, booking.Status);
        Assert.Null(attempt.CompletedAtUtc);
        Assert.Null(payment.CompletedAtUtc);
        Assert.Null(booking.PaidAtUtc);
    }

    [Fact]
    public void Reconcile_WhenGatewayStatusIsFailed_ShouldFailAttemptAndPaymentWithoutPayingBooking()
    {
        // Arrange
        DomainBooking booking = BookingTestFactory.CreatePendingPaymentBooking();
        Payment payment = CreatePayment(booking);
        PaymentAttempt attempt = AddPendingAttempt(payment, "operation-failed", "external-failed");
        DateTimeOffset observedAtUtc = UtcNow.AddMinutes(1);

        // Act
        Result result = PaymentReconciler.Reconcile(
            payment,
            attempt,
            booking,
            PaymentGatewayStatus.Failed,
            observedAtUtc);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(PaymentAttemptStatus.Failed, attempt.Status);
        Assert.Equal(PaymentStatus.Failed, payment.Status);
        Assert.Equal(BookingStatus.PendingPayment, booking.Status);
        Assert.Equal(observedAtUtc, attempt.CompletedAtUtc);
        Assert.Null(payment.CompletedAtUtc);
        Assert.Null(booking.PaidAtUtc);
    }

    [Fact]
    public void Reconcile_WhenGatewayStatusIsCancelled_ShouldCancelAttemptAndPaymentWithoutPayingBooking()
    {
        // Arrange
        DomainBooking booking = BookingTestFactory.CreatePendingPaymentBooking();
        Payment payment = CreatePayment(booking);
        PaymentAttempt attempt = AddPendingAttempt(payment, "operation-cancelled", "external-cancelled");
        DateTimeOffset observedAtUtc = UtcNow.AddMinutes(1);

        // Act
        Result result = PaymentReconciler.Reconcile(
            payment,
            attempt,
            booking,
            PaymentGatewayStatus.Cancelled,
            observedAtUtc);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(PaymentAttemptStatus.Cancelled, attempt.Status);
        Assert.Equal(PaymentStatus.Cancelled, payment.Status);
        Assert.Equal(BookingStatus.PendingPayment, booking.Status);
        Assert.Equal(observedAtUtc, attempt.CompletedAtUtc);
        Assert.Equal(observedAtUtc, payment.CompletedAtUtc);
    }

    [Fact]
    public void Reconcile_WhenSucceededWasAlreadyReconciled_ShouldBeIdempotent()
    {
        // Arrange
        DomainBooking booking = BookingTestFactory.CreatePendingPaymentBooking();
        Payment payment = CreatePayment(booking);
        PaymentAttempt attempt = AddPendingAttempt(payment, "operation-idempotent", "external-idempotent");
        DateTimeOffset firstObservedAtUtc = UtcNow.AddMinutes(1);

        Result firstResult = PaymentReconciler.Reconcile(
            payment,
            attempt,
            booking,
            PaymentGatewayStatus.Succeeded,
            firstObservedAtUtc);

        Assert.True(firstResult.IsSuccess);

        DateTimeOffset secondObservedAtUtc = firstObservedAtUtc.AddMinutes(5);

        // Act
        Result secondResult = PaymentReconciler.Reconcile(
            payment,
            attempt,
            booking,
            PaymentGatewayStatus.Succeeded,
            secondObservedAtUtc);

        // Assert
        Assert.True(secondResult.IsSuccess);
        Assert.Equal(PaymentAttemptStatus.Succeeded, attempt.Status);
        Assert.Equal(PaymentStatus.Succeeded, payment.Status);
        Assert.Equal(BookingStatus.Paid, booking.Status);
        Assert.Equal(firstObservedAtUtc, attempt.CompletedAtUtc);
        Assert.Equal(firstObservedAtUtc, payment.CompletedAtUtc);
        Assert.Equal(firstObservedAtUtc, booking.PaidAtUtc);
    }

    [Fact]
    public void Reconcile_WhenBookingIsAlreadyCompleted_ShouldNotMoveBookingBackToPaid()
    {
        // Arrange
        DomainBooking booking = BookingTestFactory.CreatePendingPaymentBooking();
        Payment payment = CreatePayment(booking);
        PaymentAttempt attempt = AddPendingAttempt(payment, "operation-completed", "external-completed");
        DateTimeOffset firstObservedAtUtc = UtcNow.AddMinutes(1);

        Result firstReconciliation = PaymentReconciler.Reconcile(
            payment,
            attempt,
            booking,
            PaymentGatewayStatus.Succeeded,
            firstObservedAtUtc);

        Assert.True(firstReconciliation.IsSuccess);
        Assert.Equal(BookingStatus.Paid, booking.Status);

        Result completeResult = booking.Complete(BookingTestTime.CompletedAtUtc);
        Assert.True(completeResult.IsSuccess);
        Assert.Equal(BookingStatus.Completed, booking.Status);

        // Act
        Result secondReconciliation = PaymentReconciler.Reconcile(
            payment,
            attempt,
            booking,
            PaymentGatewayStatus.Succeeded,
            firstObservedAtUtc.AddMinutes(5));

        // Assert
        Assert.True(secondReconciliation.IsSuccess);
        Assert.Equal(BookingStatus.Completed, booking.Status);
        Assert.Equal(PaymentStatus.Succeeded, payment.Status);
        Assert.Equal(PaymentAttemptStatus.Succeeded, attempt.Status);
    }

    [Fact]
    public void Reconcile_WhenBookingIsCancelledAndGatewayReportsSucceeded_ShouldReturnConflictWithoutPartialMutation()
    {
        // Arrange
        DomainBooking booking = BookingTestFactory.CreatePendingPaymentBooking();
        Result cancelBookingResult = booking.Cancel(BookingTestTime.CancelledAtUtc);
        Assert.True(cancelBookingResult.IsSuccess);
        Assert.Equal(BookingStatus.Cancelled, booking.Status);

        Payment payment = CreatePayment(booking);
        PaymentAttempt attempt = AddPendingAttempt(
            payment,
            "operation-booking-cancelled",
            "external-booking-cancelled");

        // Act
        Result result = PaymentReconciler.Reconcile(
            payment,
            attempt,
            booking,
            PaymentGatewayStatus.Succeeded,
            UtcNow.AddMinutes(1));

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(PaymentReconciliationErrors.BookingStateConflict(booking.Id, BookingStatus.Cancelled), result.Error);
        Assert.Equal(BookingStatus.Cancelled, booking.Status);
        Assert.Equal(PaymentStatus.Pending, payment.Status);
        Assert.Equal(PaymentAttemptStatus.Pending, attempt.Status);
        Assert.Null(payment.CompletedAtUtc);
        Assert.Null(attempt.CompletedAtUtc);
    }

    [Theory]
    [InlineData(PaymentGatewayStatus.Failed)]
    [InlineData(PaymentGatewayStatus.Cancelled)]
    [InlineData(PaymentGatewayStatus.Pending)]
    public void Reconcile_WhenPaymentAlreadySucceededAndLateNonSuccessArrives_ShouldRemainSucceeded(
        PaymentGatewayStatus lateStatus)
    {
        // Arrange
        DomainBooking booking = BookingTestFactory.CreatePendingPaymentBooking();
        Payment payment = CreatePayment(booking);
        PaymentAttempt attempt = AddPendingAttempt(
            payment,
            "operation-succeeded-monotonic",
            "external-succeeded-monotonic");

        DateTimeOffset succeededAtUtc = UtcNow.AddMinutes(1);

        Result succeededResult = PaymentReconciler.Reconcile(
            payment,
            attempt,
            booking,
            PaymentGatewayStatus.Succeeded,
            succeededAtUtc);

        Assert.True(succeededResult.IsSuccess);

        // Act
        Result lateResult = PaymentReconciler.Reconcile(
            payment,
            attempt,
            booking,
            lateStatus,
            succeededAtUtc.AddMinutes(5));

        // Assert
        Assert.True(lateResult.IsSuccess);
        Assert.Equal(PaymentStatus.Succeeded, payment.Status);
        Assert.Equal(PaymentAttemptStatus.Succeeded, attempt.Status);
        Assert.Equal(BookingStatus.Paid, booking.Status);
        Assert.Equal(succeededAtUtc, payment.CompletedAtUtc);
        Assert.Equal(succeededAtUtc, attempt.CompletedAtUtc);
    }

    [Theory]
    [InlineData(PaymentGatewayStatus.Failed)]
    [InlineData(PaymentGatewayStatus.Cancelled)]
    public void Reconcile_WhenNonSuccessWasObservedBeforeSucceeded_ShouldPromoteToSucceeded(
        PaymentGatewayStatus firstStatus)
    {
        // Arrange
        DomainBooking booking = BookingTestFactory.CreatePendingPaymentBooking();
        Payment payment = CreatePayment(booking);
        PaymentAttempt attempt = AddPendingAttempt(payment, "operation-late-success", "external-late-success");

        Result firstResult = PaymentReconciler.Reconcile(
            payment,
            attempt,
            booking,
            firstStatus,
            UtcNow.AddMinutes(1));

        Assert.True(firstResult.IsSuccess);
        Assert.Equal(BookingStatus.PendingPayment, booking.Status);

        // Act
        DateTimeOffset succeededAtUtc = UtcNow.AddMinutes(2);
        Result succeededResult = PaymentReconciler.Reconcile(
            payment,
            attempt,
            booking,
            PaymentGatewayStatus.Succeeded,
            succeededAtUtc);

        // Assert
        Assert.True(succeededResult.IsSuccess);
        Assert.Equal(PaymentStatus.Succeeded, payment.Status);
        Assert.Equal(PaymentAttemptStatus.Succeeded, attempt.Status);
        Assert.Equal(BookingStatus.Paid, booking.Status);
        Assert.Equal(succeededAtUtc, payment.CompletedAtUtc);
        Assert.Equal(succeededAtUtc, attempt.CompletedAtUtc);
    }

    [Theory]
    [InlineData(PaymentGatewayStatus.Failed, PaymentGatewayStatus.Cancelled, PaymentStatus.Failed, PaymentAttemptStatus.Failed)]
    [InlineData(PaymentGatewayStatus.Cancelled, PaymentGatewayStatus.Failed, PaymentStatus.Cancelled, PaymentAttemptStatus.Cancelled)]
    public void Reconcile_WhenDifferentNonSuccessTerminalObservationArrivesLate_ShouldKeepFirstTerminalState(
        PaymentGatewayStatus firstStatus,
        PaymentGatewayStatus lateStatus,
        PaymentStatus expectedPaymentStatus,
        PaymentAttemptStatus expectedAttemptStatus)
    {
        // Arrange
        DomainBooking booking = BookingTestFactory.CreatePendingPaymentBooking();
        Payment payment = CreatePayment(booking);
        PaymentAttempt attempt = AddPendingAttempt(payment, "operation-terminal-order", "external-terminal-order");
        DateTimeOffset firstObservedAtUtc = UtcNow.AddMinutes(1);

        Result firstResult = PaymentReconciler.Reconcile(
            payment,
            attempt,
            booking,
            firstStatus,
            firstObservedAtUtc);

        Assert.True(firstResult.IsSuccess);

        // Act
        Result lateResult = PaymentReconciler.Reconcile(
            payment,
            attempt,
            booking,
            lateStatus,
            firstObservedAtUtc.AddMinutes(5));

        // Assert
        Assert.True(lateResult.IsSuccess);
        Assert.Equal(expectedPaymentStatus, payment.Status);
        Assert.Equal(expectedAttemptStatus, attempt.Status);
        Assert.Equal(BookingStatus.PendingPayment, booking.Status);
        Assert.Equal(firstObservedAtUtc, attempt.CompletedAtUtc);
    }

    private static Payment CreatePayment(DomainBooking booking)
    {
        return BookingTestFactory.CreatePayment(
            booking,
            amount: booking.PriceSnapshot!.TotalPrice.Amount,
            currency: booking.PriceSnapshot!.TotalPrice.Currency,
            createdAtUtc: UtcNow.AddMinutes(-1));
    }

    private static PaymentAttempt AddPendingAttempt(
        Payment payment,
        string idempotencyKey,
        string externalReference)
    {
        Result<PaymentAttempt> attemptResult = payment.AddAttempt(
            idempotencyKey,
            externalReference,
            UtcNow);

        Assert.True(attemptResult.IsSuccess);
        return attemptResult.Value;
    }
}
