
using Bookify.Services.Booking.Application.Abstractions.BackgroundJobs;
using Bookify.Services.Booking.Application.Abstractions.Persistence;
using Bookify.Services.Booking.Application.Abstractions.Time;
using Bookify.Services.Booking.Application.Notifications;
using Bookify.Services.Booking.Application.Observability;
using Bookify.Services.Booking.Application.Outbox;
using Bookify.Services.Booking.Domain.Bookings.Events;
using Bookify.Services.Booking.Domain.Shared;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace Bookify.Services.Booking.Application.BackgroundJobs.Notifications;

public sealed class OutboxNotificationJob : IBackgroundJob
{
    private const string UnsupportedEventCode = "Outbox.UnsupportedEventType";
    private const string InvalidPayloadCode = "Outbox.InvalidPayload";
    private const string DeliveryFailureCode = "Outbox.NotificationDeliveryFailed";
    private const string UnexpectedFailureCode = "Outbox.UnexpectedFailure";

    private readonly IOutboxMessageReader _outboxReader;
    private readonly IOutboxMessageStore _outboxStore;
    private readonly BookingNotificationService _notificationService;
    private readonly IBackgroundJobClaimManager _claimManager;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;
    private readonly ILogger<OutboxNotificationJob> _logger;
    private readonly OutboxRetryOptions _retryOptions;

    public OutboxNotificationJob(
        IOutboxMessageReader outboxReader,
        IOutboxMessageStore outboxStore,
        BookingNotificationService notificationService,
        IBackgroundJobClaimManager claimManager,
        IUnitOfWork unitOfWork,
        IClock clock,
        ILogger<OutboxNotificationJob> logger,
        IOptions<OutboxRetryOptions> retryOptions)
    {
        _outboxReader = outboxReader ??
            throw new ArgumentNullException(nameof(outboxReader));

        _outboxStore = outboxStore ??
            throw new ArgumentNullException(nameof(outboxStore));

        _notificationService = notificationService ??
            throw new ArgumentNullException(nameof(notificationService));

        _claimManager = claimManager ??
            throw new ArgumentNullException(nameof(claimManager));

        _unitOfWork = unitOfWork ??
            throw new ArgumentNullException(nameof(unitOfWork));

        _clock = clock ??
            throw new ArgumentNullException(nameof(clock));

        _logger = logger ??
            throw new ArgumentNullException(nameof(logger));

        ArgumentNullException.ThrowIfNull(retryOptions);
        _retryOptions = retryOptions.Value;
    }

    public async Task<int> ExecuteBatchAsync(
        int batchSize,
        CancellationToken cancellationToken = default)
    {
        if (batchSize <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(batchSize), batchSize, "Batch size must be greater than zero.");
        }

        IReadOnlyList<PendingOutboxMessageReadModel> messages = await _outboxReader.GetPendingMessagesAsync(
                batchSize,
                cancellationToken);

        int processedCount = 0;

        foreach (PendingOutboxMessageReadModel message in messages)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                IBackgroundJobClaim? claim = await _claimManager.TryAcquireAsync(
                        BackgroundJobClaimKeys.OutboxNotification,
                        message.Id,
                        cancellationToken);

                if (claim is null)
                {
                    continue;
                }

                await using (claim)
                {
                    bool processed = await ProcessMessageAsync(
                        message,
                        cancellationToken);

                    if (processed)
                    {
                        processedCount++;
                    }
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                BookingMetrics.RecordJobItemFailed(BackgroundJobClaimKeys.OutboxNotification);

                _logger.LogError(
                    ApplicationLogEvents.OutboxMessageUnhandledException,
                    "Unexpected failure processing outbox message " +
                    "{OutboxMessageId}. ExceptionType: {ExceptionType}.",
                    message.Id,
                    exception.GetType().Name);
            }
        }

        return processedCount;
    }

    private async Task<bool> ProcessMessageAsync(
        PendingOutboxMessageReadModel message,
        CancellationToken cancellationToken)
    {
        if (string.Equals(message.EventType, nameof(BookingCreatedDomainEvent), StringComparison.Ordinal))
        {
            await MarkProcessedAsync(message.Id, cancellationToken);
            return true;
        }

        if (!TryMapNotificationType(message.EventType, out BookingNotificationType notificationType))
        {
            _logger.LogWarning(
                ApplicationLogEvents.OutboxUnsupportedEventType,
                "Outbox message {OutboxMessageId} has " +
                "an unsupported event type.",
                message.Id);

            await RecordFailureAsync(
                message.Id,
                UnsupportedEventCode,
                isPermanent: true,
                cancellationToken);

            return false;
        }

        if (!TryGetBookingId(message.Payload, out Guid bookingId))
        {
            _logger.LogWarning(
                ApplicationLogEvents.OutboxInvalidPayload,
                "Outbox message {OutboxMessageId} " +
                "has an invalid payload.",
                message.Id);

            await RecordFailureAsync(
                message.Id,
                InvalidPayloadCode,
                isPermanent: true,
                cancellationToken);

            return false;
        }

        Result<BookingNotificationDispatchOutcome> deliveryResult;

        try
        {
            deliveryResult = await _notificationService.SendAsync(bookingId, notificationType, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogError(
                ApplicationLogEvents.OutboxMessageUnhandledException,
                "Notification processing failed for outbox message " +
                "{OutboxMessageId}. ExceptionType: {ExceptionType}.",
                message.Id,
                exception.GetType().Name);

            await RecordFailureAsync(
                message.Id,
                UnexpectedFailureCode,
                isPermanent: false,
                cancellationToken);

            return false;
        }

        if (deliveryResult.IsFailure)
        {
            if (deliveryResult.Error == BookingNotificationErrors.DeliveryDisabled)
            {
                await DeferAsync(message.Id, cancellationToken);
                return false;
            }

            _logger.LogWarning(
                ApplicationLogEvents.OutboxNotificationDeliveryFailed,
                "Notification delivery failed for outbox message " +
                "{OutboxMessageId}. ErrorCode: {ErrorCode}.",
                message.Id,
                deliveryResult.Error.Code);

            await RecordFailureAsync(
                message.Id,
                DeliveryFailureCode,
                isPermanent: false,
                cancellationToken);

            return false;
        }

        await MarkProcessedAsync(message.Id, cancellationToken);
        return true;
    }

    private async Task MarkProcessedAsync(
        Guid messageId,
        CancellationToken cancellationToken)
    {
        await _outboxStore.MarkProcessedAsync(messageId, _clock.UtcNow, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private async Task RecordFailureAsync(
        Guid messageId,
        string failureCode,
        bool isPermanent,
        CancellationToken cancellationToken)
    {
        OutboxFailureOutcome outcome = await _outboxStore.RecordFailureAsync(
            messageId,
            _clock.UtcNow,
            failureCode,
            _retryOptions.MaxAttempts,
            _retryOptions.BaseDelay,
            _retryOptions.MaxDelay,
            isPermanent,
            cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        BookingMetrics.RecordJobItemFailed(BackgroundJobClaimKeys.OutboxNotification);

        if (outcome.IsDeadLettered)
        {
            BookingMetrics.RecordOutboxTransition("dead_lettered");

            _logger.LogError(
                ApplicationLogEvents.OutboxDeadLettered,
                "Outbox message {OutboxMessageId} was dead-lettered " +
                "after {AttemptCount} failed attempt(s). " +
                "FailureCode: {FailureCode}.",
                messageId,
                outcome.AttemptCount,
                failureCode);

            return;
        }

        BookingMetrics.RecordOutboxTransition("retry_scheduled");

        _logger.LogWarning(
            ApplicationLogEvents.OutboxRetryScheduled,
            "Outbox message {OutboxMessageId} scheduled " +
            "for retry after attempt {AttemptCount}. " +
            "NextAttemptAtUtc: {NextAttemptAtUtc}. " +
            "FailureCode: {FailureCode}.",
            messageId,
            outcome.AttemptCount,
            outcome.NextAttemptAtUtc,
            failureCode);
    }

    private async Task DeferAsync(
        Guid messageId,
        CancellationToken cancellationToken)
    {
        DateTimeOffset nextAttemptAtUtc = _clock.UtcNow.Add(_retryOptions.DisabledDelay);

        await _outboxStore.DeferAsync(messageId, nextAttemptAtUtc, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        BookingMetrics.RecordOutboxTransition("deferred");

        _logger.LogInformation(
            ApplicationLogEvents.OutboxDeliveryDeferred,
            "Outbox message {OutboxMessageId} deferred because " +
            "notification delivery is disabled. " +
            "NextAttemptAtUtc: {NextAttemptAtUtc}.",
            messageId,
            nextAttemptAtUtc);
    }

    private static bool TryMapNotificationType(
        string eventType,
        out BookingNotificationType notificationType)
    {
        if (string.Equals(eventType, nameof(BookingApprovedDomainEvent), StringComparison.Ordinal))
        {
            notificationType = BookingNotificationType.BookingApproved;

            return true;
        }

        if (string.Equals(eventType, nameof(BookingPaidDomainEvent), StringComparison.Ordinal))
        {
            notificationType = BookingNotificationType.PaymentConfirmed;

            return true;
        }

        if (string.Equals(eventType, nameof(BookingCancelledDomainEvent), StringComparison.Ordinal))
        {
            notificationType = BookingNotificationType.BookingCancelled;

            return true;
        }

        notificationType = default;
        return false;
    }

    private static bool TryGetBookingId(
        string payload,
        out Guid bookingId)
    {
        bookingId = Guid.Empty;

        try
        {
            using JsonDocument document = JsonDocument.Parse(payload);

            JsonElement root = document.RootElement;

            if (!root.TryGetProperty("bookingId", out JsonElement bookingIdElement))
            {
                return false;
            }

            return bookingIdElement.TryGetGuid(out bookingId);
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
