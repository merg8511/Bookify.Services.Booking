using Bookify.Services.Booking.Application.Abstractions.Messaging;
using Bookify.Services.Booking.Application.Abstractions.Payments;
using Bookify.Services.Booking.Application.Abstractions.Persistence;
using Bookify.Services.Booking.Application.Abstractions.Persistence.Repositories;
using Bookify.Services.Booking.Application.Abstractions.Time;
using Bookify.Services.Booking.Application.Observability;
using Bookify.Services.Booking.Application.Payments.Reconciliation;
using Bookify.Services.Booking.Domain.Bookings;
using Bookify.Services.Booking.Domain.Payments;
using Bookify.Services.Booking.Domain.Payments.Errors;
using Bookify.Services.Booking.Domain.Shared;
using System.Security.Cryptography;
using System.Text;

using DomainBooking = Bookify.Services.Booking.Domain.Bookings.Booking;

namespace Bookify.Services.Booking.Application.Payments.Initiate;

public sealed class InitiatePaymentCommandHandler
    : ICommandHandler<InitiatePaymentCommand, InitiatePaymentResponse>
{
    private static readonly TimeSpan RecoveryWindow = TimeSpan.FromHours(23);
    private readonly IBookingRepository _bookingRepository;
    private readonly IPaymentRepository _paymentRepository;
    private readonly IPaymentGateway _paymentGateway;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ITransactionManager _transactionManager;
    private readonly IPaymentInitiationLock _paymentInitiationLock;
    private readonly IClock _clock;

    public InitiatePaymentCommandHandler(
        IBookingRepository bookingRepository,
        IPaymentRepository paymentRepository,
        IPaymentGateway paymentGateway,
        IUnitOfWork unitOfWork,
        ITransactionManager transactionManager,
        IPaymentInitiationLock paymentInitiationLock,
        IClock clock)
    {
        _bookingRepository = bookingRepository;
        _paymentRepository = paymentRepository;
        _paymentGateway = paymentGateway;
        _unitOfWork = unitOfWork;
        _transactionManager = transactionManager;
        _paymentInitiationLock = paymentInitiationLock;
        _clock = clock;
    }

    public async Task<Result<InitiatePaymentResponse>> HandleAsync(
        InitiatePaymentCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        string incomingKey = command.IdempotencyKey?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(incomingKey))
        {
            return Result<InitiatePaymentResponse>.Failure(
                InitiatePaymentErrors.IdempotencyKeyRequired);
        }

        string operationKey = CreateOperationKey(command.BookingId, incomingKey);

        Payment? reservedPayment = null;

        // PHASE 1:
        // Commit the provider operation identity BEFORE the call.
        await using (ITransaction preparation = await _transactionManager.BeginAsync(cancellationToken))
        {
            try
            {
                bool locked = await _paymentInitiationLock.TryAcquireAsync(
                        command.BookingId,
                        cancellationToken);

                if (!locked)
                {
                    return await RollbackFailureAsync(
                        preparation,
                        InitiatePaymentErrors.BookingNotFound(command.BookingId),
                        cancellationToken);
                }

                DomainBooking? booking = await _bookingRepository.GetByIdAsync(
                        command.BookingId,
                        cancellationToken);

                if (booking is null)
                {
                    return await RollbackFailureAsync(
                        preparation,
                        InitiatePaymentErrors.BookingNotFound(command.BookingId),
                        cancellationToken);
                }

                Payment? payment = await _paymentRepository.GetByBookingIdAsync(
                        booking.Id,
                        cancellationToken);

                if (payment is not null)
                {
                    PaymentAttempt? existingAttempt = FindAttempt(payment, operationKey);

                    if (existingAttempt is not null)
                    {
                        if (HasRecoveryWindowExpired(existingAttempt.CreatedAtUtc))
                        {
                            return await RollbackFailureAsync(
                                preparation,
                                InitiatePaymentErrors.SessionRecoveryWindowExpired,
                                cancellationToken);
                        }

                        Result<CreatePaymentAttemptResponse> session =
                            await _paymentGateway.CreatePaymentAttemptAsync(
                                    new CreatePaymentAttemptRequest(
                                        booking.Id,
                                        payment.Amount,
                                        operationKey),
                                    cancellationToken);

                        if (session.IsFailure)
                        {
                            return await RollbackFailureAsync(
                                preparation,
                                session.Error,
                                cancellationToken);
                        }

                        return await ReconcileExistingAsync(
                            preparation,
                            booking,
                            payment,
                            existingAttempt,
                            session.Value,
                            cancellationToken);
                    }
                }

                if (booking.Status != BookingStatus.PendingPayment)
                {
                    return await RollbackFailureAsync(preparation,
                        InitiatePaymentErrors.BookingNotPendingPayment(booking.Status),
                        cancellationToken);
                }

                if (payment is not null)
                {
                    if (payment.Status == PaymentStatus.Succeeded)
                    {
                        return await RollbackFailureAsync(
                            preparation,
                            InitiatePaymentErrors.PaymentAlreadySucceeded,
                            cancellationToken);
                    }

                    if (payment.Attempts.Any(attempt =>
                        attempt.Status == PaymentAttemptStatus.Pending))
                    {
                        return await RollbackFailureAsync(
                            preparation,
                            PaymentErrors.ActiveAttemptAlreadyExists,
                            cancellationToken);
                    }

                    if (payment.PendingInitiationKey is not null)
                    {
                        if (!string.Equals(
                            payment.PendingInitiationKey,
                            operationKey,
                            StringComparison.Ordinal))
                        {
                            return await RollbackFailureAsync(
                                preparation,
                                InitiatePaymentErrors.PaymentOutcomeUncertain,
                                cancellationToken);
                        }

                        DateTimeOffset reservedAt = payment.PendingInitiationCreatedAtUtc ??
                            throw new InvalidOperationException(
                                "An initiation reservation requires a date.");

                        if (HasRecoveryWindowExpired(reservedAt))
                        {
                            return await RollbackFailureAsync(
                                preparation,
                                InitiatePaymentErrors.SessionRecoveryWindowExpired,
                                cancellationToken);
                        }

                        // Same key: recover the durable operation.
                        // Do not generate a new key or change the amount.
                    }
                    else if (payment.Attempts.Count == 0)
                    {
                        // Fail closed for historical Payments created
                        // before the durable reservation was introduced.
                        return await RollbackFailureAsync(
                            preparation,
                            InitiatePaymentErrors.PaymentOutcomeUncertain,
                            cancellationToken);
                    }
                    else
                    {
                        // A new operation is allowed only after
                        // all previous attempts are non-pending.
                        payment.ReserveInitiation(operationKey, _clock.UtcNow);

                        await _unitOfWork.SaveChangesAsync(cancellationToken);
                    }
                }
                else
                {
                    if (booking.PriceSnapshot is null)
                    {
                        return await RollbackFailureAsync(
                            preparation,
                            InitiatePaymentErrors.PriceSnapshotMissing(booking.Id),
                            cancellationToken);
                    }

                    Result<Payment> paymentResult = Payment.Create(
                        booking.Id,
                        booking.PriceSnapshot.TotalPrice,
                        _clock.UtcNow);

                    if (paymentResult.IsFailure)
                    {
                        return await RollbackFailureAsync(
                            preparation,
                            paymentResult.Error,
                            cancellationToken);
                    }

                    payment = paymentResult.Value;

                    payment.ReserveInitiation(operationKey, _clock.UtcNow);

                    _paymentRepository.Add(payment);

                    await _unitOfWork.SaveChangesAsync(cancellationToken);
                }

                reservedPayment = payment;

                // After this commit, the operation identity survives
                // a crash or network timeout.
                await preparation.CommitAsync(cancellationToken);
            }
            catch
            {
                await preparation.RollbackAsync(CancellationToken.None);
                throw;
            }
        }

        Payment activePayment = reservedPayment ??
            throw new InvalidOperationException(
                "Payment initiation was not reserved.");

        // PHASE 2:
        // The external call executes OUTSIDE a database transaction.
        // Retries inside StripePaymentGateway must preserve this key.
        Result<CreatePaymentAttemptResponse> gatewayResult = await _paymentGateway.CreatePaymentAttemptAsync(
                new CreatePaymentAttemptRequest(
                    command.BookingId,
                    activePayment.Amount,
                    operationKey),
                cancellationToken);

        if (gatewayResult.IsFailure)
        {
            // Never erase the reservation on an uncertain result.
            return Result<InitiatePaymentResponse>.Failure(gatewayResult.Error);
        }

        // PHASE 3:
        // Reacquire the booking lock and persist the provider reference.
        await using ITransaction completion = await _transactionManager.BeginAsync(cancellationToken);

        try
        {
            bool locked =
                await _paymentInitiationLock.TryAcquireAsync(
                    command.BookingId,
                    cancellationToken);

            if (!locked)
            {
                throw new InvalidOperationException("The reserved booking could not be locked.");
            }

            DomainBooking booking = await _bookingRepository.GetByIdAsync(
                    command.BookingId,
                    cancellationToken) ??
                throw new InvalidOperationException("The reserved booking could not be found.");

            Payment payment =
                await _paymentRepository.GetByBookingIdAsync(
                    command.BookingId,
                    cancellationToken) ??
                throw new InvalidOperationException("The reserved payment could not be found.");

            PaymentAttempt? existingAttempt = FindAttempt(payment, operationKey);

            if (existingAttempt is not null)
            {
                // A concurrent request might have completed
                // the same provider operation first.
                return await ReconcileExistingAsync(
                    completion,
                    booking,
                    payment,
                    existingAttempt,
                    gatewayResult.Value,
                    cancellationToken);
            }

            if (!string.Equals(
                payment.PendingInitiationKey,
                operationKey,
                StringComparison.Ordinal))
            {
                return await RollbackFailureAsync(
                    completion,
                    InitiatePaymentErrors.PaymentOutcomeUncertain,
                    cancellationToken);
            }

            if (booking.Status != BookingStatus.PendingPayment)
            {
                return await RollbackFailureAsync(
                    completion,
                    InitiatePaymentErrors.BookingNotPendingPayment(
                        booking.Status),
                    cancellationToken);
            }

            DateTimeOffset observedAt = _clock.UtcNow;

            Result<PaymentAttempt> attemptResult = payment.AddAttempt(
                    operationKey,
                    gatewayResult.Value.ExternalReference,
                    observedAt);

            if (attemptResult.IsFailure)
            {
                return await RollbackFailureAsync(
                    completion,
                    attemptResult.Error,
                    cancellationToken);
            }

            PaymentAttempt attempt = attemptResult.Value;

            PaymentStatus previousStatus = payment.Status;

            Result reconciliationResult = PaymentReconciler.Reconcile(
                    payment,
                    attempt,
                    booking,
                    gatewayResult.Value.Status,
                    observedAt);

            if (reconciliationResult.IsFailure)
            {
                return await RollbackFailureAsync(
                    completion,
                    reconciliationResult.Error,
                    cancellationToken);
            }

            // Clear only after the real external reference
            // is attached to a valid PaymentAttempt.
            payment.CompleteInitiation(operationKey);

            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await completion.CommitAsync(cancellationToken);

            BookingMetrics.RecordPaymentTransition(
                previousStatus,
                payment.Status);

            return Result<InitiatePaymentResponse>.Success(
                ToResponse(
                    payment,
                    attempt,
                    gatewayResult.Value.ClientSecret));
        }
        catch
        {
            await completion.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    private async Task<Result<InitiatePaymentResponse>>
        ReconcileExistingAsync(
            ITransaction transaction,
            DomainBooking booking,
            Payment payment,
            PaymentAttempt attempt,
            CreatePaymentAttemptResponse providerResponse,
            CancellationToken cancellationToken)
    {
        // M20.1 — an idempotency reference mismatch is
        // an unexpected consistency violation, not a Result.
        if (!string.Equals(
            attempt.ExternalReference,
            providerResponse.ExternalReference,
            StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "The provider returned a different reference " +
                "for an existing payment initiation.");
        }

        PaymentStatus previousPaymentStatus = payment.Status;
        PaymentAttemptStatus previousAttemptStatus = attempt.Status;
        BookingStatus previousBookingStatus = booking.Status;

        Result reconciliationResult = PaymentReconciler.Reconcile(
            payment,
            attempt,
            booking,
            providerResponse.Status,
            _clock.UtcNow);

        if (reconciliationResult.IsFailure)
        {
            return await RollbackFailureAsync(
                transaction,
                reconciliationResult.Error,
                cancellationToken);
        }

        if (payment.Status != previousPaymentStatus ||
            attempt.Status != previousAttemptStatus ||
            booking.Status != previousBookingStatus)
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);

        BookingMetrics.RecordPaymentTransition(
            previousPaymentStatus,
            payment.Status);

        return Result<InitiatePaymentResponse>.Success(
            ToResponse(
                payment,
                attempt,
                providerResponse.ClientSecret));
    }

    private bool HasRecoveryWindowExpired(
        DateTimeOffset createdAtUtc)
    {
        return _clock.UtcNow >=
            createdAtUtc.Add(RecoveryWindow);
    }

    private static PaymentAttempt? FindAttempt(
        Payment payment,
        string operationKey)
    {
        return payment.Attempts.FirstOrDefault(
            attempt => string.Equals(
                attempt.IdempotencyKey,
                operationKey,
                StringComparison.Ordinal));
    }

    private static string CreateOperationKey(
        Guid bookingId,
        string incomingIdempotencyKey)
    {
        string value = $"{bookingId:N}:{incomingIdempotencyKey}";

        byte[] bytes = Encoding.UTF8.GetBytes(value);
        byte[] hash = SHA256.HashData(bytes);

        return $"bookify-payment-{Convert.ToHexString(hash).ToLowerInvariant()}";
    }

    private static InitiatePaymentResponse ToResponse(
        Payment payment,
        PaymentAttempt attempt,
        string clientSecret)
    {
        return new InitiatePaymentResponse(
            payment.Id,
            attempt.Id,
            attempt.ExternalReference,
            attempt.Status,
            attempt.Amount.Amount,
            attempt.Amount.Currency,
            clientSecret);
    }

    private static async Task<Result<InitiatePaymentResponse>>
        RollbackFailureAsync(
            ITransaction transaction,
            Error error,
            CancellationToken cancellationToken)
    {
        await transaction.RollbackAsync(cancellationToken);

        return Result<InitiatePaymentResponse>.Failure(error);
    }
}
