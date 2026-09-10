using Bookify.Services.Booking.Application.Abstractions.Payments;
using Bookify.Services.Booking.Application.Payments.Reconciliation;
using Bookify.Services.Booking.Domain.Bookings;
using Bookify.Services.Booking.Domain.Bookings.Pricing;
using Bookify.Services.Booking.Domain.Bookings.ValueObjects;
using Bookify.Services.Booking.Domain.Payments;
using Bookify.Services.Booking.Domain.Properties;
using Bookify.Services.Booking.Domain.Shared;
using Bookify.Services.Booking.Domain.Shared.ValueObjects;

using DomainBooking = Bookify.Services.Booking.Domain.Bookings.Booking;

namespace Bookify.Services.Booking.Application.Tests.Payments.Reconciliation;

public sealed class PaymentReconcilerTests
{
    private static readonly DateTimeOffset UtcNow =
        new(
            2026,
            9,
            4,
            20,
            0,
            0,
            TimeSpan.Zero);

    [Fact]
    public void Reconcile_WhenGatewayStatusIsSucceeded_ShouldSucceedPaymentAttemptPaymentAndBooking()
    {
        // Arrange
        DomainBooking booking =
            CreatePendingPaymentBooking();

        Payment payment =
            CreatePayment(
                booking);

        PaymentAttempt attempt =
            AddPendingAttempt(
                payment,
                "operation-succeeded",
                "external-succeeded");

        DateTimeOffset observedAtUtc =
            UtcNow.AddMinutes(
                1);

        // Act
        Result result =
            PaymentReconciler.Reconcile(
                payment,
                attempt,
                booking,
                PaymentGatewayStatus.Succeeded,
                observedAtUtc);

        // Assert
        Assert.True(
            result.IsSuccess);

        Assert.Equal(
            PaymentAttemptStatus.Succeeded,
            attempt.Status);

        Assert.Equal(
            PaymentStatus.Succeeded,
            payment.Status);

        Assert.Equal(
            BookingStatus.Paid,
            booking.Status);

        Assert.Equal(
            observedAtUtc,
            attempt.CompletedAtUtc);

        Assert.Equal(
            observedAtUtc,
            payment.CompletedAtUtc);
    }

    [Fact]
    public void Reconcile_WhenGatewayStatusIsPending_ShouldNotChangeCurrentState()
    {
        // Arrange
        DomainBooking booking =
            CreatePendingPaymentBooking();

        Payment payment =
            CreatePayment(
                booking);

        PaymentAttempt attempt =
            AddPendingAttempt(
                payment,
                "operation-pending",
                "external-pending");

        // Act
        Result result =
            PaymentReconciler.Reconcile(
                payment,
                attempt,
                booking,
                PaymentGatewayStatus.Pending,
                UtcNow.AddMinutes(1));

        // Assert
        Assert.True(
            result.IsSuccess);

        Assert.Equal(
            PaymentAttemptStatus.Pending,
            attempt.Status);

        Assert.Equal(
            PaymentStatus.Pending,
            payment.Status);

        Assert.Equal(
            BookingStatus.PendingPayment,
            booking.Status);

        Assert.Null(
            attempt.CompletedAtUtc);

        Assert.Null(
            payment.CompletedAtUtc);
    }

    [Fact]
    public void Reconcile_WhenGatewayStatusIsFailed_ShouldFailAttemptAndPaymentWithoutPayingBooking()
    {
        // Arrange
        DomainBooking booking =
            CreatePendingPaymentBooking();

        Payment payment =
            CreatePayment(
                booking);

        PaymentAttempt attempt =
            AddPendingAttempt(
                payment,
                "operation-failed",
                "external-failed");

        DateTimeOffset observedAtUtc =
            UtcNow.AddMinutes(
                1);

        // Act
        Result result =
            PaymentReconciler.Reconcile(
                payment,
                attempt,
                booking,
                PaymentGatewayStatus.Failed,
                observedAtUtc);

        // Assert
        Assert.True(
            result.IsSuccess);

        Assert.Equal(
            PaymentAttemptStatus.Failed,
            attempt.Status);

        Assert.Equal(
            PaymentStatus.Failed,
            payment.Status);

        Assert.Equal(
            BookingStatus.PendingPayment,
            booking.Status);

        Assert.Equal(
            observedAtUtc,
            attempt.CompletedAtUtc);

        Assert.Null(
            payment.CompletedAtUtc);
    }

    [Fact]
    public void Reconcile_WhenGatewayStatusIsCancelled_ShouldCancelAttemptAndPaymentWithoutPayingBooking()
    {
        // Arrange
        DomainBooking booking =
            CreatePendingPaymentBooking();

        Payment payment =
            CreatePayment(
                booking);

        PaymentAttempt attempt =
            AddPendingAttempt(
                payment,
                "operation-cancelled",
                "external-cancelled");

        DateTimeOffset observedAtUtc =
            UtcNow.AddMinutes(
                1);

        // Act
        Result result =
            PaymentReconciler.Reconcile(
                payment,
                attempt,
                booking,
                PaymentGatewayStatus.Cancelled,
                observedAtUtc);

        // Assert
        Assert.True(
            result.IsSuccess);

        Assert.Equal(
            PaymentAttemptStatus.Cancelled,
            attempt.Status);

        Assert.Equal(
            PaymentStatus.Cancelled,
            payment.Status);

        Assert.Equal(
            BookingStatus.PendingPayment,
            booking.Status);

        Assert.Equal(
            observedAtUtc,
            attempt.CompletedAtUtc);

        Assert.Equal(
            observedAtUtc,
            payment.CompletedAtUtc);
    }

    [Fact]
    public void Reconcile_WhenSucceededWasAlreadyReconciled_ShouldBeIdempotent()
    {
        // Arrange
        DomainBooking booking =
            CreatePendingPaymentBooking();

        Payment payment =
            CreatePayment(
                booking);

        PaymentAttempt attempt =
            AddPendingAttempt(
                payment,
                "operation-idempotent",
                "external-idempotent");

        DateTimeOffset firstObservedAtUtc =
            UtcNow.AddMinutes(
                1);

        Result firstResult =
            PaymentReconciler.Reconcile(
                payment,
                attempt,
                booking,
                PaymentGatewayStatus.Succeeded,
                firstObservedAtUtc);

        Assert.True(
            firstResult.IsSuccess);

        DateTimeOffset secondObservedAtUtc =
            firstObservedAtUtc.AddMinutes(
                5);

        // Act
        Result secondResult =
            PaymentReconciler.Reconcile(
                payment,
                attempt,
                booking,
                PaymentGatewayStatus.Succeeded,
                secondObservedAtUtc);

        // Assert
        Assert.True(
            secondResult.IsSuccess);

        Assert.Equal(
            PaymentAttemptStatus.Succeeded,
            attempt.Status);

        Assert.Equal(
            PaymentStatus.Succeeded,
            payment.Status);

        Assert.Equal(
            BookingStatus.Paid,
            booking.Status);

        Assert.Equal(
            firstObservedAtUtc,
            attempt.CompletedAtUtc);

        Assert.Equal(
            firstObservedAtUtc,
            payment.CompletedAtUtc);
    }

    [Fact]
    public void Reconcile_WhenBookingIsAlreadyCompleted_ShouldNotMoveBookingBackToPaid()
    {
        // Arrange
        DomainBooking booking =
            CreatePendingPaymentBooking();

        Payment payment =
            CreatePayment(
                booking);

        PaymentAttempt attempt =
            AddPendingAttempt(
                payment,
                "operation-completed",
                "external-completed");

        DateTimeOffset firstObservedAtUtc =
            UtcNow.AddMinutes(
                1);

        Result firstReconciliation =
            PaymentReconciler.Reconcile(
                payment,
                attempt,
                booking,
                PaymentGatewayStatus.Succeeded,
                firstObservedAtUtc);

        Assert.True(
            firstReconciliation.IsSuccess);

        Assert.Equal(
            BookingStatus.Paid,
            booking.Status);

        Result completeResult =
            booking.Complete();

        Assert.True(
            completeResult.IsSuccess);

        Assert.Equal(
            BookingStatus.Completed,
            booking.Status);

        // Act
        Result secondReconciliation =
            PaymentReconciler.Reconcile(
                payment,
                attempt,
                booking,
                PaymentGatewayStatus.Succeeded,
                firstObservedAtUtc.AddMinutes(5));

        // Assert
        Assert.True(
            secondReconciliation.IsSuccess);

        Assert.Equal(
            BookingStatus.Completed,
            booking.Status);

        Assert.Equal(
            PaymentStatus.Succeeded,
            payment.Status);

        Assert.Equal(
            PaymentAttemptStatus.Succeeded,
            attempt.Status);

        Assert.Equal(
            firstObservedAtUtc,
            payment.CompletedAtUtc);

        Assert.Equal(
            firstObservedAtUtc,
            attempt.CompletedAtUtc);
    }

    [Fact]
    public void Reconcile_WhenBookingIsCancelledAndGatewayReportsSucceeded_ShouldReturnConflictWithoutPartialMutation()
    {
        // Arrange
        DomainBooking booking =
            CreatePendingPaymentBooking();

        Result cancelBookingResult =
            booking.Cancel();

        Assert.True(
            cancelBookingResult.IsSuccess);

        Assert.Equal(
            BookingStatus.Cancelled,
            booking.Status);

        Payment payment =
            CreatePayment(
                booking);

        PaymentAttempt attempt =
            AddPendingAttempt(
                payment,
                "operation-booking-cancelled",
                "external-booking-cancelled");

        // Act
        Result result =
            PaymentReconciler.Reconcile(
                payment,
                attempt,
                booking,
                PaymentGatewayStatus.Succeeded,
                UtcNow.AddMinutes(1));

        // Assert
        Assert.True(
            result.IsFailure);

        Assert.Equal(
            PaymentReconciliationErrors
                .BookingStateConflict(
                    booking.Id,
                    BookingStatus.Cancelled),
            result.Error);

        Assert.Equal(
            BookingStatus.Cancelled,
            booking.Status);

        Assert.Equal(
            PaymentStatus.Pending,
            payment.Status);

        Assert.Equal(
            PaymentAttemptStatus.Pending,
            attempt.Status);

        Assert.Null(
            payment.CompletedAtUtc);

        Assert.Null(
            attempt.CompletedAtUtc);
    }

    private static DomainBooking
        CreatePendingPaymentBooking()
    {
        RentableUnit rentableUnit =
            CreateRentableUnit();

        StayPeriod stayPeriod =
            StayPeriod.Create(
                new DateOnly(
                    2026,
                    9,
                    10),
                new DateOnly(
                    2026,
                    9,
                    12))
            .Value;

        GuestCount guestCount =
            GuestCount.Create(
                2)
            .Value;

        PriceSnapshot priceSnapshot =
            CreatePriceSnapshot();

        Result<DomainBooking> bookingResult =
            DomainBooking.Create(
                rentableUnit,
                stayPeriod,
                guestCount,
                priceSnapshot);

        Assert.True(
            bookingResult.IsSuccess);

        DomainBooking booking =
            bookingResult.Value;

        Result approveResult =
            booking.Approve();

        Assert.True(
            approveResult.IsSuccess);

        Assert.Equal(
            BookingStatus.PendingPayment,
            booking.Status);

        return booking;
    }

    [Theory]
    [InlineData(PaymentGatewayStatus.Failed)]
    [InlineData(PaymentGatewayStatus.Cancelled)]
    [InlineData(PaymentGatewayStatus.Pending)]
    public void Reconcile_WhenPaymentAlreadySucceededAndLateNonSuccessArrives_ShouldRemainSucceeded(
    PaymentGatewayStatus lateStatus)
    {
        // Arrange
        DomainBooking booking =
            CreatePendingPaymentBooking();

        Payment payment =
            CreatePayment(
                booking);

        PaymentAttempt attempt =
            AddPendingAttempt(
                payment,
                "operation-succeeded-monotonic",
                "external-succeeded-monotonic");

        DateTimeOffset succeededAtUtc =
            UtcNow.AddMinutes(1);

        Result succeededResult =
            PaymentReconciler.Reconcile(
                payment,
                attempt,
                booking,
                PaymentGatewayStatus.Succeeded,
                succeededAtUtc);

        Assert.True(
            succeededResult.IsSuccess);

        // Act
        Result lateResult =
            PaymentReconciler.Reconcile(
                payment,
                attempt,
                booking,
                lateStatus,
                succeededAtUtc.AddMinutes(5));

        // Assert
        Assert.True(
            lateResult.IsSuccess);

        Assert.Equal(
            PaymentStatus.Succeeded,
            payment.Status);

        Assert.Equal(
            PaymentAttemptStatus.Succeeded,
            attempt.Status);

        Assert.Equal(
            BookingStatus.Paid,
            booking.Status);

        Assert.Equal(
            succeededAtUtc,
            payment.CompletedAtUtc);

        Assert.Equal(
            succeededAtUtc,
            attempt.CompletedAtUtc);
    }

    [Theory]
    [InlineData(PaymentGatewayStatus.Failed)]
    [InlineData(PaymentGatewayStatus.Cancelled)]
    public void Reconcile_WhenNonSuccessWasObservedBeforeSucceeded_ShouldPromoteToSucceeded(
        PaymentGatewayStatus firstStatus)
    {
        // Arrange
        DomainBooking booking =
            CreatePendingPaymentBooking();

        Payment payment =
            CreatePayment(
                booking);

        PaymentAttempt attempt =
            AddPendingAttempt(
                payment,
                "operation-late-success",
                "external-late-success");

        Result firstResult =
            PaymentReconciler.Reconcile(
                payment,
                attempt,
                booking,
                firstStatus,
                UtcNow.AddMinutes(1));

        Assert.True(
            firstResult.IsSuccess);

        Assert.Equal(
            BookingStatus.PendingPayment,
            booking.Status);

        // Act
        DateTimeOffset succeededAtUtc =
            UtcNow.AddMinutes(2);

        Result succeededResult =
            PaymentReconciler.Reconcile(
                payment,
                attempt,
                booking,
                PaymentGatewayStatus.Succeeded,
                succeededAtUtc);

        // Assert
        Assert.True(
            succeededResult.IsSuccess);

        Assert.Equal(
            PaymentStatus.Succeeded,
            payment.Status);

        Assert.Equal(
            PaymentAttemptStatus.Succeeded,
            attempt.Status);

        Assert.Equal(
            BookingStatus.Paid,
            booking.Status);

        Assert.Equal(
            succeededAtUtc,
            payment.CompletedAtUtc);

        Assert.Equal(
            succeededAtUtc,
            attempt.CompletedAtUtc);
    }

    [Theory]
    [InlineData(
        PaymentGatewayStatus.Failed,
        PaymentGatewayStatus.Cancelled,
        PaymentStatus.Failed,
        PaymentAttemptStatus.Failed)]
    [InlineData(
        PaymentGatewayStatus.Cancelled,
        PaymentGatewayStatus.Failed,
        PaymentStatus.Cancelled,
        PaymentAttemptStatus.Cancelled)]
    public void Reconcile_WhenDifferentNonSuccessTerminalObservationArrivesLate_ShouldKeepFirstTerminalState(
        PaymentGatewayStatus firstStatus,
        PaymentGatewayStatus lateStatus,
        PaymentStatus expectedPaymentStatus,
        PaymentAttemptStatus expectedAttemptStatus)
    {
        // Arrange
        DomainBooking booking =
            CreatePendingPaymentBooking();

        Payment payment =
            CreatePayment(
                booking);

        PaymentAttempt attempt =
            AddPendingAttempt(
                payment,
                "operation-terminal-order",
                "external-terminal-order");

        DateTimeOffset firstObservedAtUtc =
            UtcNow.AddMinutes(1);

        Result firstResult =
            PaymentReconciler.Reconcile(
                payment,
                attempt,
                booking,
                firstStatus,
                firstObservedAtUtc);

        Assert.True(
            firstResult.IsSuccess);

        // Act
        Result lateResult =
            PaymentReconciler.Reconcile(
                payment,
                attempt,
                booking,
                lateStatus,
                firstObservedAtUtc.AddMinutes(5));

        // Assert
        Assert.True(
            lateResult.IsSuccess);

        Assert.Equal(
            expectedPaymentStatus,
            payment.Status);

        Assert.Equal(
            expectedAttemptStatus,
            attempt.Status);

        Assert.Equal(
            BookingStatus.PendingPayment,
            booking.Status);

        Assert.Equal(
            firstObservedAtUtc,
            attempt.CompletedAtUtc);
    }

    private static Payment CreatePayment(
        DomainBooking booking)
    {
        Result<Payment> paymentResult =
            Payment.Create(
                booking.Id,
                booking.PriceSnapshot!.TotalPrice,
                UtcNow.AddMinutes(-1));

        Assert.True(
            paymentResult.IsSuccess);

        return paymentResult.Value;
    }

    private static PaymentAttempt AddPendingAttempt(
        Payment payment,
        string idempotencyKey,
        string externalReference)
    {
        Result<PaymentAttempt> attemptResult =
            payment.AddAttempt(
                idempotencyKey,
                externalReference,
                UtcNow);

        Assert.True(
            attemptResult.IsSuccess);

        return attemptResult.Value;
    }

    private static RentableUnit
        CreateRentableUnit()
    {
        Result<RentableUnit> result =
            RentableUnit.Create(
                Guid.NewGuid(),
                "Room A",
                RentableUnitType.Room,
                maximumCapacity: 4,
                maxBaseGuests: 2);

        Assert.True(
            result.IsSuccess);

        return result.Value;
    }

    private static PriceSnapshot
        CreatePriceSnapshot()
    {
        Money accommodationPrice =
            Money.Create(
                200m,
                "USD")
            .Value;

        Money extraGuestPrice =
            Money.Create(
                0m,
                "USD")
            .Value;

        Money totalPrice =
            Money.Create(
                200m,
                "USD")
            .Value;

        return PriceSnapshot.Create(
            new PriceBreakdown(
                accommodationPrice,
                extraGuestPrice,
                totalPrice));
    }
}
