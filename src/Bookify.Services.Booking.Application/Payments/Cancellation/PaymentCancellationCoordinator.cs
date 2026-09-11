namespace Bookify.Services.Booking.Application.Payments.Cancellation;

using Bookify.Services.Booking.Application.Abstractions.Payments;
using Bookify.Services.Booking.Application.Abstractions.Time;
using Bookify.Services.Booking.Application.Payments.Reconciliation;
using Bookify.Services.Booking.Domain.Payments;
using Bookify.Services.Booking.Domain.Shared;

using DomainBooking = Bookify.Services.Booking.Domain.Bookings.Booking;

public sealed class PaymentCancellationCoordinator
{
    private readonly IPaymentGateway _paymentGateway;
    private readonly IClock _clock;

    public PaymentCancellationCoordinator(IPaymentGateway paymentGateway, IClock clock)
    {
        _paymentGateway = paymentGateway;
        _clock = clock;
    }

    public async Task<Result<PaymentCancellationOutcome>> EnsurePaymentCannotSucceedAsync(
        Payment payment,
        DomainBooking booking,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(payment);
        ArgumentNullException.ThrowIfNull(booking);

        if (payment.BookingId != booking.Id)
        {
            throw new InvalidOperationException("The payment does not belong to the supplied booking.");
        }

        PaymentAttempt? pendingAttempt = payment
            .Attempts
                .SingleOrDefault(
                    attempt => attempt.Status == PaymentAttemptStatus.Pending);

        if (pendingAttempt is null)
        {
            return HandlePaymentWithoutPendingAttempt(payment, booking);
        }

        Result<PaymentGatewayResponse> statusResult = await _paymentGateway
            .GetPaymentStatusAsync(pendingAttempt.ExternalReference, cancellationToken);

        if (statusResult.IsFailure)
        {
            return Result<PaymentCancellationOutcome>
                .Failure(statusResult.Error);
        }

        PaymentGatewayResponse observedPayment = statusResult.Value;

        Result referenceResult = ValidateExternalReference(pendingAttempt, observedPayment);

        if (referenceResult.IsFailure)
        {
            return Result<PaymentCancellationOutcome>
                 .Failure(referenceResult.Error);
        }

        Result reconciliationResult = PaymentReconciler.Reconcile(
            payment,
            pendingAttempt,
            booking,
            observedPayment.Status,
            _clock.UtcNow);

        if (reconciliationResult.IsFailure)
        {
            return Result<PaymentCancellationOutcome>
                .Failure(reconciliationResult.Error);
        }

        if (observedPayment.Status == PaymentGatewayStatus.Succeeded)
        {
            return Result<PaymentCancellationOutcome>
                .Success(PaymentCancellationOutcome.PaymentSucceeded);
        }

        if (observedPayment.Status is
            PaymentGatewayStatus.Failed or
            PaymentGatewayStatus.Cancelled)
        {
            return Result<PaymentCancellationOutcome>
                .Success(PaymentCancellationOutcome.SafeToCancelBooking);
        }

        return await CancelPendingPaymentAsync(
            payment,
            pendingAttempt,
            booking,
            cancellationToken);
    }



    private Result<PaymentCancellationOutcome> HandlePaymentWithoutPendingAttempt(
        Payment payment,
        DomainBooking booking)
    {
        if (payment.Status == PaymentStatus.Succeeded)
        {
            PaymentAttempt? succeededAttempt =
                payment.Attempts
                    .SingleOrDefault(attempt => attempt.Status == PaymentAttemptStatus.Succeeded);

            if (succeededAttempt is null)
            {
                return Result<PaymentCancellationOutcome>
                    .Failure(PaymentCancellationErrors.InconsistentSucceededState(payment.Id));
            }

            Result reconciliationResult = PaymentReconciler.Reconcile(
                payment,
                succeededAttempt,
                booking,
                PaymentGatewayStatus.Succeeded,
                _clock.UtcNow);

            if (reconciliationResult.IsFailure)
            {
                return Result<PaymentCancellationOutcome>
                    .Failure(reconciliationResult.Error);
            }

            return Result<PaymentCancellationOutcome>
                .Success(PaymentCancellationOutcome.PaymentSucceeded);
        }

        if (payment.Status is
            PaymentStatus.Failed or
            PaymentStatus.Cancelled)
        {
            return Result<PaymentCancellationOutcome>
                .Success(PaymentCancellationOutcome.SafeToCancelBooking);
        }

        if (payment.Status == PaymentStatus.Pending)
        {
            return Result<PaymentCancellationOutcome>
                .Failure(PaymentCancellationErrors.PaymentStateUncertain(payment.Id));
        }

        throw new InvalidOperationException(
            $"Unsupported payment status '{payment.Status}'");
    }

    private async Task<Result<PaymentCancellationOutcome>>
        CancelPendingPaymentAsync(
            Payment payment,
            PaymentAttempt pendingAttempt,
            DomainBooking booking,
            CancellationToken cancellationToken)
    {
        Result<PaymentGatewayResponse> cancellationResult = await _paymentGateway
            .CancelPaymentAsync(
                pendingAttempt.ExternalReference,
                cancellationToken);

        if (cancellationResult.IsFailure)
        {
            return Result<PaymentCancellationOutcome>
                .Failure(cancellationResult.Error);
        }

        PaymentGatewayResponse cancelledPayment = cancellationResult.Value;

        Result referenceResult = ValidateExternalReference(pendingAttempt, cancelledPayment);

        if (referenceResult.IsFailure)
        {
            return Result<PaymentCancellationOutcome>
                .Failure(referenceResult.Error);
        }

        Result reconciliationResult = PaymentReconciler.Reconcile(
            payment,
            pendingAttempt,
            booking,
            cancelledPayment.Status,
            _clock.UtcNow);

        if (reconciliationResult.IsFailure)
        {
            return Result<PaymentCancellationOutcome>
                .Failure(reconciliationResult.Error);
        }

        if (cancelledPayment.Status == PaymentGatewayStatus.Succeeded)
        {
            return Result<PaymentCancellationOutcome>
                .Success(PaymentCancellationOutcome.PaymentSucceeded);
        }

        if (cancelledPayment.Status is
            PaymentGatewayStatus.Failed or
            PaymentGatewayStatus.Cancelled)
        {
            return Result<PaymentCancellationOutcome>
                .Success(PaymentCancellationOutcome.SafeToCancelBooking);
        }

        return Result<PaymentCancellationOutcome>
            .Failure(PaymentCancellationErrors.CancellationNotConfirmed(
                pendingAttempt.ExternalReference,
                cancelledPayment.Status));
    }
    private static Result ValidateExternalReference(
            PaymentAttempt attempt,
            PaymentGatewayResponse providerResponse)

    {
        if (string.Equals(
            attempt.ExternalReference,
            providerResponse.ExternalReference,
            StringComparison.Ordinal))
        {
            return Result.Success();
        }

        return Result.Failure(
            PaymentCancellationErrors.ProviderReferenceMismatch(
                attempt.ExternalReference,
                providerResponse.ExternalReference));
    }

}
