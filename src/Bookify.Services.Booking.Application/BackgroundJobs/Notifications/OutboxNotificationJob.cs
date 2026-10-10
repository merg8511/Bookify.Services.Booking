using Bookify.Services.Booking.Application.Abstractions.BackgroundJobs;
using Bookify.Services.Booking.Application.Abstractions.Persistence;
using Bookify.Services.Booking.Application.Abstractions.Time;
using Bookify.Services.Booking.Application.Notifications;
using Bookify.Services.Booking.Application.Outbox;
using Bookify.Services.Booking.Domain.Bookings.Events;
using Bookify.Services.Booking.Domain.Shared;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Bookify.Services.Booking.Application.BackgroundJobs.Notifications;

public sealed class OutboxNotificationJob : IBackgroundJob
{
    private readonly IOutboxMessageReader _outboxReader;
    private readonly IOutboxMessageStore _outboxStore;
    private readonly BookingNotificationService _notificationService;
    private readonly IBackgroundJobClaimManager _claimManager;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;
    private readonly ILogger<OutboxNotificationJob> _logger;

    public OutboxNotificationJob(
        IOutboxMessageReader outboxReader,
        IOutboxMessageStore outboxStore,
        BookingNotificationService notificationService,
        IBackgroundJobClaimManager claimManager,
        IUnitOfWork unitOfWork,
        IClock clock,
        ILogger<OutboxNotificationJob> logger)
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
    }

    public async Task<int> ExecuteBatchAsync(
        int batchSize,
        CancellationToken cancellationToken = default)
    {
        if (batchSize <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(batchSize),
                batchSize,
                "Batch size must be greater than zero.");
        }

        IReadOnlyList<PendingOutboxMessageReadModel> messages = await _outboxReader
            .GetPendingMessagesAsync(batchSize, cancellationToken);

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
                    bool processed = await ProcessMessageAsync(message, cancellationToken);

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
                _logger.LogError(
                    exception,
                    "Unexpected error processing outbox message {OutboxMessageId}.",
                    message.Id);
            }
        }

        return processedCount;
    }

    private async Task<bool> ProcessMessageAsync(
        PendingOutboxMessageReadModel message,
        CancellationToken cancellationToken)
    {
        if (string.Equals(
            message.EventType, nameof(BookingCreatedDomainEvent),
            StringComparison.Ordinal))
        {
            await MarkProcessedAsync(message.Id, cancellationToken);

            return true;
        }

        if (!TryMapNotificationType(message.EventType, out BookingNotificationType notificationType))
        {
            _logger.LogWarning(
                "Outbox message {OutboxMessageId} has unsupported event type {EventType}.",
                message.Id,
                message.EventType);

            return false;
        }

        if (!TryGetBookingId(message.Payload, out Guid bookingId))
        {
            _logger.LogWarning(
                "Outbox message {OutboxMessageId} contains an invalid booking payload.",
                message.Id);

            return false;
        }

        Result<BookingNotificationDispatchOutcome> deliveryResult = await _notificationService
            .SendAsync(bookingId, notificationType, cancellationToken);

        if (deliveryResult.IsFailure)
        {
            if (deliveryResult.Error != BookingNotificationErrors.DeliveryDisabled)
            {
                _logger.LogWarning(
                    "Notification delivery failed for outbox message {OutboxMessageId}. ErrorCode: {ErrorCode}.",
                    message.Id, deliveryResult.Error.Code);
            }

            return false;
        }

        await MarkProcessedAsync(message.Id, cancellationToken);

        return true;
    }

    private async Task MarkProcessedAsync(Guid messageId, CancellationToken cancellationToken)
    {
        await _outboxStore.MarkProcessedAsync(messageId, _clock.UtcNow, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private static bool TryMapNotificationType(string eventType, out BookingNotificationType notificationType)
    {
        if (string.Equals(eventType, nameof(BookingApprovedDomainEvent),
            StringComparison.Ordinal))
        {
            notificationType = BookingNotificationType.BookingApproved;

            return true;
        }

        if (string.Equals(eventType, nameof(BookingPaidDomainEvent),
            StringComparison.Ordinal))
        {
            notificationType = BookingNotificationType.PaymentConfirmed;

            return true;
        }

        if (string.Equals(eventType, nameof(BookingCancelledDomainEvent),
            StringComparison.Ordinal))
        {
            notificationType = default;

            return true;
        }

        notificationType = default;
        return false;
    }

    private static bool TryGetBookingId(string payload, out Guid bookingId)
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
