using Bookify.Services.Booking.Application.Abstractions.Messaging;
using Bookify.Services.Booking.Application.Abstractions.Payments;
using Bookify.Services.Booking.Application.Abstractions.Persistence;
using Bookify.Services.Booking.Application.Abstractions.Persistence.Repositories;
using Bookify.Services.Booking.Application.Abstractions.Time;
using Bookify.Services.Booking.Application.Payments.Reconciliation;
using Bookify.Services.Booking.Domain.Payments;
using Bookify.Services.Booking.Domain.Shared;
using System.Text.Json;

using DomainBooking = Bookify.Services.Booking.Domain.Bookings.Booking;

namespace Bookify.Services.Booking.Application.Payments.Webhooks.Stripe;

public sealed class ProcessStripeWebhookCommandHandler
    : ICommandHandler<ProcessStripeWebhookCommand>
{
    private const string BookingMetadataKey = "bookify_booking_id";
    private readonly IBookingRepository _bookingRepository;
    private readonly IPaymentRepository _paymentRepository;
    private readonly IStripeWebhookSignatureVerifier _signatureVerifier;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ITransactionManager _transactionManager;
    private readonly IClock _clock;

    public ProcessStripeWebhookCommandHandler(
        IBookingRepository bookingRepository,
        IPaymentRepository paymentRepository,
        IStripeWebhookSignatureVerifier signatureVerifier,
        IUnitOfWork unitOfWork,
        ITransactionManager transactionManager,
        IClock clock)
    {
        _bookingRepository = bookingRepository;
        _paymentRepository = paymentRepository;
        _signatureVerifier = signatureVerifier;
        _unitOfWork = unitOfWork;
        _transactionManager = transactionManager;
        _clock = clock;
    }

    public async Task<Result> HandleAsync(
        ProcessStripeWebhookCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (string.IsNullOrWhiteSpace(command.RawBody))
        {
            return Result.Failure(StripeWebhookErrors.InvalidPayload);
        }

        if (string.IsNullOrWhiteSpace(command.SignatureHeader))
        {
            return Result.Failure(StripeWebhookSignatureErrors.SignatureRequired);
        }

        Result signatureResult = _signatureVerifier.Verify(command.RawBody, command.SignatureHeader, _clock.UtcNow);

        if (signatureResult.IsFailure)
        {
            return signatureResult;
        }

        try
        {
            using JsonDocument document = JsonDocument.Parse(command.RawBody);
            JsonElement root = document.RootElement;

            if (root.ValueKind != JsonValueKind.Object)
            {
                return Result.Failure(StripeWebhookErrors.InvalidPayload);
            }

            if (!TryGetRequiredString(root, "type", out string eventType))
            {
                return Result.Failure(StripeWebhookErrors.InvalidPayload);
            }

            bool isSupportedEvent = StripeWebhookEventTypes.TryMapToGatewayStatus(
                eventType, out PaymentGatewayStatus observedStatus);

            if (!isSupportedEvent)
            {
                return Result.Success();
            }

            if (!root.TryGetProperty("data", out JsonElement dataElement) ||
                dataElement.ValueKind != JsonValueKind.Object)
            {
                return Result.Failure(StripeWebhookErrors.InvalidPayload);
            }

            if (!dataElement.TryGetProperty("object", out JsonElement paymentIntentElement) ||
                paymentIntentElement.ValueKind != JsonValueKind.Object)
            {
                return Result.Failure(StripeWebhookErrors.InvalidPayload);
            }

            if (!paymentIntentElement.TryGetProperty("metadata", out JsonElement metadataElement) ||
                metadataElement.ValueKind != JsonValueKind.Object)
            {
                return Result.Success();
            }

            if (!TryGetRequiredString(metadataElement, BookingMetadataKey, out string bookingIdValue))
            {
                return Result.Success();
            }

            if (!Guid.TryParse(bookingIdValue, out Guid bookingId))
            {
                return Result.Failure(StripeWebhookErrors.InvalidBookingId(bookingIdValue));
            }

            if (!TryGetRequiredString(paymentIntentElement, "id", out string extenalReference))
            {
                return Result.Failure(StripeWebhookErrors.InvalidPayload);
            }

            return await ProcessRelevantEventAsync(
                bookingId,
                extenalReference,
                observedStatus,
                cancellationToken);
        }
        catch (JsonException)
        {
            return Result.Failure(StripeWebhookErrors.InvalidPayload);
        }
    }

    private async Task<Result> ProcessRelevantEventAsync(
        Guid bookingId,
        string externalReference,
        PaymentGatewayStatus observedStatus,
        CancellationToken cancellationToken)
    {
        await using ITransaction transaction = await _transactionManager
            .BeginAsync(cancellationToken);

        try
        {
            DomainBooking? booking = await _bookingRepository
                .GetByIdAsync(bookingId, cancellationToken);

            if (booking is null)
            {
                return await RollbackFailureAsync(
                    transaction,
                    StripeWebhookErrors.BookingNotFound(bookingId),
                    cancellationToken);
            }

            Payment? payment = await _paymentRepository
                .GetByBookingIdAsync(bookingId, cancellationToken);

            if (payment is null)
            {
                return await RollbackFailureAsync(
                    transaction,
                    StripeWebhookErrors.PaymentNotFound(booking.Id), cancellationToken);
            }

            PaymentAttempt? attempt = payment.Attempts
                .FirstOrDefault(
                    currentAttempt =>
                        string.Equals(
                            currentAttempt.ExternalReference,
                            externalReference,
                            StringComparison.Ordinal));

            if (attempt is null)
            {
                return await RollbackFailureAsync(
                    transaction,
                    StripeWebhookErrors.PaymentAttemptNotFound(payment.Id, externalReference), cancellationToken);
            }

            Result reconciliationResult = PaymentReconciler.Reconcile(
                payment,
                attempt,
                booking,
                observedStatus,
                _clock.UtcNow);

            if (reconciliationResult.IsFailure)
            {
                return await RollbackFailureAsync(
                    transaction,
                    reconciliationResult.Error,
                    cancellationToken);
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return Result.Success();
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private static bool TryGetRequiredString(
        JsonElement element,
        string propertyName,
        out string value)
    {
        value = string.Empty;

        if (!element.TryGetProperty(propertyName, out JsonElement propertyElement))
        {
            return false;
        }

        if (propertyElement.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        value = propertyElement.GetString()?.Trim() ?? string.Empty;

        return !string.IsNullOrWhiteSpace(value);
    }

    private static async Task<Result> RollbackFailureAsync(
        ITransaction transaction,
        Error error,
        CancellationToken cancellationToken)
    {
        await transaction.RollbackAsync(cancellationToken);

        return Result.Failure(error);
    }
}
