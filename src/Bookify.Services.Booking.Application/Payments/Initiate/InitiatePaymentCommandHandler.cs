using Bookify.Services.Booking.Application.Abstractions.Messaging;
using Bookify.Services.Booking.Application.Abstractions.Payments;
using Bookify.Services.Booking.Application.Abstractions.Persistence;
using Bookify.Services.Booking.Application.Abstractions.Persistence.Repositories;
using Bookify.Services.Booking.Application.Abstractions.Time;
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

            DomainBooking? booking =
                await _bookingRepository
                    .GetByIdAsync(
                        command.BookingId,
                        cancellationToken);

            if (booking is null)
            {
                return await RollbackFailureAsync(
                    transaction,
                    InitiatePaymentErrors
                        .BookingNotFound(
                            command.BookingId),
                    cancellationToken);
            }

            if (booking.Status !=
                BookingStatus.PendingPayment)
            {
                return await RollbackFailureAsync(
                    transaction,
                    InitiatePaymentErrors
                        .BookingNotPendingPayment(
                            booking.Status),
                    cancellationToken);
            }

            string operationKey = CreateOperationKey(booking.Id, incomingIdempotencyKey);

            Payment? payment = await _paymentRepository
                    .GetByBookingIdAsync(
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
                    await transaction.CommitAsync(cancellationToken);

                    return Result<InitiatePaymentResponse>
                        .Success(ToResponse(payment, existingAttempt));
                }

                if (payment.Status == PaymentStatus.Succeeded)
                {
                    return await RollbackFailureAsync(
                        transaction,
                        InitiatePaymentErrors
                            .PaymentAlreadySucceeded,
                        cancellationToken);
                }

                if (payment.Status == PaymentStatus.Cancelled)
                {
                    return await RollbackFailureAsync(
                        transaction,
                        InitiatePaymentErrors
                            .PaymentCancelled,
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

            Result<PaymentGatewayResponse> gatewayResult =
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

            PaymentGatewayResponse gatewayResponse = gatewayResult.Value;

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

            Result gatewayStatusResult =
                ApplyGatewayStatus(
                    payment,
                    attempt,
                    gatewayResponse.Status,
                    gatewayObservedAtUtc);

            if (gatewayStatusResult.IsFailure)
            {
                await transaction.CommitAsync(cancellationToken);

                return Result<InitiatePaymentResponse>
                    .Failure(gatewayStatusResult.Error);
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);

            return Result<InitiatePaymentResponse>
                .Success(
                    ToResponse(
                        payment,
                        attempt));
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);

            throw;
        }
    }

    private static Result ApplyGatewayStatus(
        Payment payment,
        PaymentAttempt attempt,
        PaymentGatewayStatus gatewayStatus,
        DateTimeOffset observedAtUtc)
    {
        return gatewayStatus switch
        {
            PaymentGatewayStatus.Pending =>
                Result.Success(),

            PaymentGatewayStatus.Succeeded =>
                payment.MarkAttemptAsSucceeded(
                    attempt.ExternalReference,
                    observedAtUtc),

            PaymentGatewayStatus.Failed =>
                payment.MarkAttemptAsFailed(
                    attempt.ExternalReference,
                    observedAtUtc),

            PaymentGatewayStatus.Cancelled =>
                payment.CancelAttempt(
                    attempt.ExternalReference,
                    observedAtUtc),

            _ =>
                throw new InvalidOperationException(
                    $"Unsupported payment gateway status '{gatewayStatus}'.")
        };
    }
    private static string CreateOperationKey(
        Guid bookingId,
        string incomingIdempotencyKey)
    {
        string value = $"{bookingId:N}:{incomingIdempotencyKey}";

        byte[] bytes = Encoding.UTF8.GetBytes(value);

        byte[] hash = SHA256.HashData(bytes);

        return $"bookify-payment-" + $"{Convert.ToHexString(hash).ToLowerInvariant()}";
    }

    private static InitiatePaymentResponse ToResponse(
        Payment payment,
        PaymentAttempt attempt)
    {
        return new InitiatePaymentResponse(
            payment.Id,
            attempt.Id,
            attempt.ExternalReference,
            attempt.Status,
            attempt.Amount.Amount,
            attempt.Amount.Currency);
    }

    private static async Task<
        Result<InitiatePaymentResponse>>
        RollbackFailureAsync(
            ITransaction transaction,
            Error error,
            CancellationToken cancellationToken)
    {
        await transaction
            .RollbackAsync(cancellationToken);

        return Result<InitiatePaymentResponse>
            .Failure(error);
    }
}
