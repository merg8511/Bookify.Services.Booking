using Bookify.Services.Booking.Application.Abstractions.Messaging;
using Bookify.Services.Booking.Application.Abstractions.Payments;
using Bookify.Services.Booking.Application.Abstractions.Persistence;
using Bookify.Services.Booking.Application.Abstractions.Persistence.Repositories;
using Bookify.Services.Booking.Application.Abstractions.Time;
using Bookify.Services.Booking.Application.Payments.Reconciliation;
using Bookify.Services.Booking.Domain.Bookings;
using Bookify.Services.Booking.Domain.Bookings.Pricing;
using Bookify.Services.Booking.Domain.Payments;
using Bookify.Services.Booking.Domain.Payments.Errors;
using Bookify.Services.Booking.Domain.Shared;
using System.Security.Cryptography;
using System.Text;

using DomainBooking = Bookify.Services.Booking.Domain.Bookings.Booking;

namespace Bookify.Services.Booking.Application.Payments.Initiate;

public sealed class InitiatePaymentCommandHandler
    : ICommandHandler<
        InitiatePaymentCommand,
        InitiatePaymentResponse>
{
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

        string incomingIdempotencyKey = command.IdempotencyKey?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(incomingIdempotencyKey))
        {
            return Result<InitiatePaymentResponse>
                .Failure(InitiatePaymentErrors.IdempotencyKeyRequired);
        }

        await using ITransaction transaction = await _transactionManager.BeginAsync(cancellationToken);

        try
        {
            bool bookingLocked = await _paymentInitiationLock
                    .TryAcquireAsync(
                        command.BookingId,
                        cancellationToken);

            if (!bookingLocked)
            {
                return await RollbackFailureAsync(
                    transaction,
                    InitiatePaymentErrors
                        .BookingNotFound(
                            command.BookingId),
                    cancellationToken);
            }

            DomainBooking? booking = await _bookingRepository
                    .GetByIdAsync(
                        command.BookingId,
                        cancellationToken);

            if (booking is null)
            {
                return await RollbackFailureAsync(transaction, InitiatePaymentErrors
                        .BookingNotFound(command.BookingId), cancellationToken);
            }

            string operationKey = CreateOperationKey(booking.Id, incomingIdempotencyKey);

            Payment? payment = await _paymentRepository.GetByBookingIdAsync(
                        booking.Id,
                        cancellationToken);

            if (payment is not null)
            {
                PaymentAttempt? existingAttempt =
                    payment.Attempts
                        .FirstOrDefault(
                            attempt =>
                                string.Equals(
                                    attempt.IdempotencyKey,
                                    operationKey,
                                    StringComparison.Ordinal));

                if (existingAttempt is not null)
                {
                    Result<CreatePaymentAttemptResponse> existingSessionResult = await _paymentGateway
                        .CreatePaymentAttemptAsync(
                            new CreatePaymentAttemptRequest(
                                booking.Id,
                                payment.Amount,
                                operationKey),
                            cancellationToken);

                    if (existingSessionResult.IsFailure)
                    {
                        return await RollbackFailureAsync(
                            transaction,
                            existingSessionResult.Error,
                            cancellationToken);
                    }

                    CreatePaymentAttemptResponse existingSession = existingSessionResult.Value;

                    if (!string.Equals(
                        existingAttempt.ExternalReference,
                        existingSession.ExternalReference,
                        StringComparison.Ordinal))
                    {
                        return await RollbackFailureAsync(
                            transaction,
                            PaymentGatewayErrors
                                .IdempotencyResultMismatch,
                            cancellationToken);
                    }

                    DateTimeOffset observedAtUtc = _clock.UtcNow;
                    PaymentStatus paymentStatusBefore = payment.Status;
                    PaymentAttemptStatus attemptStatusBefore = existingAttempt.Status;
                    BookingStatus bookingStatusBefore = booking.Status;

                    Result existingAttemptReconciliationResult = PaymentReconciler
                        .Reconcile(
                            payment,
                            existingAttempt,
                            booking,
                            existingSession.Status,
                            observedAtUtc);

                    if (existingAttemptReconciliationResult.IsFailure)
                    {
                        return await RollbackFailureAsync(
                            transaction,
                            existingAttemptReconciliationResult.Error,
                            cancellationToken);
                    }

                    bool reconciliationChangedState =
                        payment.Status != paymentStatusBefore ||
                        existingAttempt.Status != attemptStatusBefore ||
                        booking.Status != bookingStatusBefore;

                    if (reconciliationChangedState)
                    {
                        await _unitOfWork.SaveChangesAsync(cancellationToken);
                    }

                    await transaction.CommitAsync(cancellationToken);

                    return Result<InitiatePaymentResponse>
                        .Success(ToResponse(payment, existingAttempt, existingSession.ClientSecret));
                }
            }

            if (booking.Status != BookingStatus.PendingPayment)
            {
                return await RollbackFailureAsync(
                    transaction,
                    InitiatePaymentErrors
                        .BookingNotPendingPayment(booking.Status),
                    cancellationToken);
            }

            if (payment is not null)
            {
                if (payment.Status == PaymentStatus.Succeeded)
                {
                    return await RollbackFailureAsync(
                        transaction,
                        InitiatePaymentErrors
                            .PaymentAlreadySucceeded,
                        cancellationToken);
                }

                bool hasPendingAttempt =
                payment.Attempts.Any(
                    attempt =>
                        attempt.Status ==
                        PaymentAttemptStatus.Pending);

                if (hasPendingAttempt)
                {
                    return await RollbackFailureAsync(
                        transaction,
                        PaymentErrors
                            .ActiveAttemptAlreadyExists,
                        cancellationToken);
                }
            }
            else
            {
                PriceSnapshot? priceSnapshot = booking.PriceSnapshot;

                if (priceSnapshot is null)
                {
                    return await RollbackFailureAsync(
                        transaction,
                        InitiatePaymentErrors
                            .PriceSnapshotMissing(
                                booking.Id),
                        cancellationToken);
                }

                Result<Payment> paymentResult =
                    Payment.Create(
                        booking.Id,
                        priceSnapshot.TotalPrice,
                        _clock.UtcNow);

                if (paymentResult.IsFailure)
                {
                    return await RollbackFailureAsync(
                        transaction,
                        paymentResult.Error,
                        cancellationToken);
                }

                payment = paymentResult.Value;

                _paymentRepository.Add(payment);

                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }

            Result<CreatePaymentAttemptResponse> gatewayResult =
                await _paymentGateway
                    .CreatePaymentAttemptAsync(
                        new CreatePaymentAttemptRequest(
                            booking.Id,
                            payment.Amount,
                            operationKey),
                        cancellationToken);

            if (gatewayResult.IsFailure)
            {
                await transaction.CommitAsync(cancellationToken);

                return Result<InitiatePaymentResponse>
                    .Failure(gatewayResult.Error);
            }

            CreatePaymentAttemptResponse gatewayResponse = gatewayResult.Value;
            DateTimeOffset gatewayObservedAtUtc = _clock.UtcNow;

            Result<PaymentAttempt> attemptResult =
                payment.AddAttempt(
                    operationKey,
                    gatewayResponse.ExternalReference,
                    gatewayObservedAtUtc);

            if (attemptResult.IsFailure)
            {
                await transaction.CommitAsync(cancellationToken);

                return Result<InitiatePaymentResponse>
                    .Failure(attemptResult.Error);
            }

            PaymentAttempt attempt = attemptResult.Value;

            Result newAttemptReconciliationResult = PaymentReconciler
                .Reconcile(
                    payment,
                    attempt,
                    booking,
                    gatewayResponse.Status,
                    gatewayObservedAtUtc);

            if (newAttemptReconciliationResult.IsFailure)
            {
                return await RollbackFailureAsync(
                    transaction,
                    newAttemptReconciliationResult.Error,
                    cancellationToken);
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);

            return Result<InitiatePaymentResponse>
                .Success(
                    ToResponse(
                        payment,
                        attempt,
                        gatewayResponse.ClientSecret));
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);

            throw;
        }
    }

    private static string CreateOperationKey(Guid bookingId, string incomingIdempotencyKey)
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

    private static async Task<Result<InitiatePaymentResponse>> RollbackFailureAsync(
            ITransaction transaction,
            Error error,
            CancellationToken cancellationToken)
    {
        await transaction.RollbackAsync(cancellationToken);

        return Result<InitiatePaymentResponse>.Failure(error);
    }
}
