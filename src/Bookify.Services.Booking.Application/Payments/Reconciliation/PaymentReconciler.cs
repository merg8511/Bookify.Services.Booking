using Bookify.Services.Booking.Application.Abstractions.Payments;
using Bookify.Services.Booking.Domain.Bookings;
using Bookify.Services.Booking.Domain.Payments;
using Bookify.Services.Booking.Domain.Shared;
using DomainBooking = Bookify.Services.Booking.Domain.Bookings.Booking;

namespace Bookify.Services.Booking.Application.Payments.Reconciliation;

public static class PaymentReconciler
{
    public static Result Reconcile(
        Payment payment,
        PaymentAttempt attempt,
        DomainBooking booking,
        PaymentGatewayStatus observedStatus,
        DateTimeOffset observedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(payment);
        ArgumentNullException.ThrowIfNull(attempt);
        ArgumentNullException.ThrowIfNull(booking);

        if (payment.BookingId != booking.Id)
        {
            throw new InvalidOperationException("The payment does not belong to the supplied booking.");
        }

        bool attemptBelingsToPayment = payment
            .Attempts.Any(currentAttempt => currentAttempt.Id == attempt.Id);

        if (!attemptBelingsToPayment)
        {
            throw new InvalidOperationException("The payment attempt does not belong to the supplied payment.");
        }

        if (payment.Status == PaymentStatus.Succeeded &&
            observedStatus != PaymentGatewayStatus.Succeeded)
        {
            return ReconcileAlreadySucceededPayment(payment, booking);
        }

        return observedStatus switch
        {
            PaymentGatewayStatus.Pending =>
                    ReconcilePending(
                        payment,
                        attempt),

            PaymentGatewayStatus.Succeeded =>
                    ReconcileSucceeded(
                    payment,
                    attempt,
                    booking,
                    observedAtUtc),

            PaymentGatewayStatus.Failed =>
                ReconcileFailed(
                    payment,
                    attempt,
                    observedAtUtc),

            PaymentGatewayStatus.Cancelled =>
                ReconcileCancelled(
                    payment,
                    attempt,
                    observedAtUtc),
            _ =>
                throw new InvalidOperationException($"Unsupported payment gateway status '{observedStatus}'.")
        };
    }

    private static Result ReconcilePending(Payment payment, PaymentAttempt attempt)
    {
        if (attempt.Status == PaymentAttemptStatus.Succeeded &&
            payment.Status != PaymentStatus.Succeeded)
        {
            throw new InvalidOperationException(
                "A succeeded payment attempt requires the payment to be succeeded.");
        }

        return Result.Success();
    }

    private static Result ReconcileSucceeded(
        Payment payment,
        PaymentAttempt attempt,
        DomainBooking booking,
        DateTimeOffset observedAtUtc)
    {
        if (booking.Status is BookingStatus.PendingApproval or
            BookingStatus.Cancelled)
        {
            return Result.Failure(
                PaymentReconciliationErrors
                    .BookingStateConflict(booking.Id, booking.Status));
        }

        if (booking.Status is BookingStatus.PendingApproval or
            BookingStatus.Cancelled)
        {
            return Result.Failure(PaymentReconciliationErrors.BookingStateConflict(booking.Id, booking.Status));
        }

        if (attempt.Status == PaymentAttemptStatus.Succeeded)
        {
            if (payment.Status != PaymentStatus.Succeeded)
            {
                throw new InvalidOperationException(
                    "A succeeded payment attempt requires the payment to be succeeded.");
            }

            return ReconcileBookingAsPaid(booking);
        }

        if (payment.Status == PaymentStatus.Succeeded)
        {
            throw new InvalidOperationException("A payment that already succeeded cannot accept " +
                "a second succeeded payment attempt.");
        }

        Result paymentResult = payment
            .MarkAttemptAsSucceeded(attempt.ExternalReference, observedAtUtc);

        if (paymentResult.IsFailure)
        {
            return paymentResult;
        }

        return ReconcileBookingAsPaid(booking);
    }

    private static Result ReconcileFailed(
        Payment payment,
        PaymentAttempt attempt,
        DateTimeOffset observedAtUtc)
    {
        if (attempt.Status == PaymentAttemptStatus.Succeeded)
        {
            if (payment.Status != PaymentStatus.Succeeded)
            {
                throw new InvalidOperationException("A succeeded payment requires the payment to be succeeded.");
            }

            return Result.Success();
        }

        if (attempt.Status is PaymentAttemptStatus.Failed or
            PaymentAttemptStatus.Cancelled)
        {
            return Result.Success();
        }

        return payment.MarkAttemptAsFailed(attempt.ExternalReference, observedAtUtc);
    }

    private static Result ReconcileCancelled(
        Payment payment,
        PaymentAttempt attempt,
        DateTimeOffset observedAtUtc)
    {
        if (attempt.Status == PaymentAttemptStatus.Succeeded)
        {
            if (payment.Status != PaymentStatus.Succeeded)
            {
                throw new InvalidOperationException("A succeeded payment attempt requires the payment to be succeeded.");
            }

            return Result.Success();
        }

        if (attempt.Status is PaymentAttemptStatus.Failed or
            PaymentAttemptStatus.Cancelled)
        {
            return Result.Success();
        }

        return payment.CancelAttempt(attempt.ExternalReference, observedAtUtc);
    }

    private static Result ReconcileAlreadySucceededPayment(Payment payment, DomainBooking booking)
    {
        bool hasSucceededAttempt = payment.Attempts.Any(
            attempt => attempt.Status == PaymentAttemptStatus.Succeeded);

        if (!hasSucceededAttempt)
        {
            throw new InvalidOperationException("A succeeded payment requires at least one succeeded payment attempt.");
        }

        return ReconcileBookingAsPaid(booking);
    }

    private static Result ReconcileBookingAsPaid(DomainBooking booking)
    {
        return booking.Status switch
        {
            BookingStatus.PendingPayment => booking.MarkAsPaid(),
            BookingStatus.Paid => Result.Success(),
            BookingStatus.Completed => Result.Success(),

            _ =>
                Result.Failure(PaymentReconciliationErrors.BookingStateConflict(booking.Id, booking.Status))
        };
    }
}
