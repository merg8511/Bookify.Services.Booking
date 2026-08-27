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
    private readonly IClock _clock;

    public InitiatePaymentCommandHandler(
        IBookingRepository bookingRepository,
        IPaymentRepository paymentRepository,
        IPaymentGateway paymentGateway,
        IUnitOfWork unitOfWork,
        IClock clock)
    {
        _bookingRepository = bookingRepository;
        _paymentRepository = paymentRepository;
        _paymentGateway = paymentGateway;
        _unitOfWork = unitOfWork;
        _clock = clock;
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

        DomainBooking? booking = await _bookingRepository
                .GetByIdAsync(
                    command.BookingId,
                    cancellationToken);

        if (booking is null)
        {
            return Result<InitiatePaymentResponse>
                .Failure(InitiatePaymentErrors.BookingNotFound(command.BookingId));
        }

        if (booking.Status != BookingStatus.PendingPayment)
        {
            return Result<InitiatePaymentResponse>
                .Failure(InitiatePaymentErrors.BookingNotPendingPayment(booking.Status));
        }

        string operationKey = CreateOperationKey(booking.Id, incomingIdempotencyKey);

        Payment? payment = await _paymentRepository.GetByBookingIdAsync(booking.Id, cancellationToken);

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
                return Result<InitiatePaymentResponse>
                    .Success(ToResponse(payment, existingAttempt));
            }

            if (payment.Status == PaymentStatus.Succeeded)
            {
                return Result<InitiatePaymentResponse>
                    .Failure(InitiatePaymentErrors.PaymentAlreadySucceeded);
            }

            if (payment.Status == PaymentStatus.Cancelled)
            {
                return Result<InitiatePaymentResponse>
                    .Failure(InitiatePaymentErrors.PaymentCancelled);
            }

            bool hasPendingAttempt =
                payment.Attempts.Any(
                    attempt =>
                        attempt.Status ==
                        PaymentAttemptStatus.Pending);

            if (hasPendingAttempt)
            {
                return Result<InitiatePaymentResponse>
                    .Failure(PaymentErrors.ActiveAttemptAlreadyExists);
            }
        }
        else
        {
            PriceSnapshot? priceSnapshot = booking.PriceSnapshot;

            if (priceSnapshot is null)
            {
                return Result<InitiatePaymentResponse>
                    .Failure(InitiatePaymentErrors.PriceSnapshotMissing(booking.Id));
            }

            Result<Payment> paymentResult =
                Payment.Create(
                    booking.Id,
                    priceSnapshot.TotalPrice,
                    _clock.UtcNow);

            if (paymentResult.IsFailure)
            {
                return Result<InitiatePaymentResponse>
                    .Failure(paymentResult.Error);
            }

            payment = paymentResult.Value;

            _paymentRepository.Add(payment);

            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        Result<PaymentGatewayResponse>
            gatewayResult =
                await _paymentGateway
                    .CreatePaymentAttemptAsync(
                        new CreatePaymentAttemptRequest(
                            booking.Id,
                            payment.Amount,
                            operationKey),
                        cancellationToken);

        if (gatewayResult.IsFailure)
        {
            return Result<InitiatePaymentResponse>
                .Failure(gatewayResult.Error);
        }

        PaymentGatewayResponse gatewayResponse = gatewayResult.Value;

        Result<PaymentAttempt> attemptResult =
            payment.AddAttempt(
                operationKey,
                gatewayResponse.ExternalReference,
                _clock.UtcNow);

        if (attemptResult.IsFailure)
        {
            return Result<InitiatePaymentResponse>
                .Failure(attemptResult.Error);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<InitiatePaymentResponse>
            .Success(ToResponse(payment,
                    attemptResult.Value));
    }
}
