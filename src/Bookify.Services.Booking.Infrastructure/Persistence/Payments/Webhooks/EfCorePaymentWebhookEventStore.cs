using Bookify.Services.Booking.Application.Abstractions.Payments.Webhooks;
using Bookify.Services.Booking.Application.Payments.Webhooks;
using Bookify.Services.Booking.Domain.Shared;
using Microsoft.EntityFrameworkCore;

namespace Bookify.Services.Booking.Infrastructure.Persistence.Payments.Webhooks;

internal sealed class EfCorePaymentWebhookEventStore : IPaymentWebhookEventStore
{
    private readonly BookingDbContext _dbContext;

    public EfCorePaymentWebhookEventStore(BookingDbContext dbContext)
    {
        _dbContext = dbContext
            ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public async Task<Result<PaymentWebhookEventPreparation>> PrepareAsync(
        string provider,
        string eventId,
        string eventType,
        DateTimeOffset receivedAtUtc,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(provider);
        ArgumentException.ThrowIfNullOrWhiteSpace(eventId);
        ArgumentException.ThrowIfNullOrWhiteSpace(eventType);

        Guid candidateId = Guid.NewGuid();

        int affectedRows = await _dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT INTO payment_webhook_events
            (
                id,
                provider,
                event_id,
                event_type,
                received_at_utc,
                processed_at_utc,
                processing_status,
                error_code,
                error_message
            )
            VALUES
            (
                {candidateId},
                {provider},
                {eventId},
                {eventType},
                {receivedAtUtc},
                NULL,
                'Processing',
                NULL,
                NULL
            )
            ON CONFLICT
            (
                event_id
            )
            DO NOTHING;
            """,
            cancellationToken);

        PaymentWebhookEvent webhookEvent;

        if (affectedRows == 1)
        {
            webhookEvent = await _dbContext.PaymentWebhookEvents
                .SingleAsync(currentEvent => currentEvent.Id == candidateId, cancellationToken);
        }
        else
        {
            webhookEvent = await _dbContext.PaymentWebhookEvents
                .SingleAsync(currentEvent => currentEvent.EventId == eventId, cancellationToken);
        }

        bool sameIdentity =
            string.Equals(webhookEvent.Provider, provider, StringComparison.Ordinal) &&
            string.Equals(webhookEvent.EventType, eventType, StringComparison.Ordinal);

        if (!sameIdentity)
        {
            return Result<PaymentWebhookEventPreparation>
                .Failure(PaymentWebhookPersistenceErrors.EventIdentityMismatch(eventId));
        }

        if (affectedRows == 1)
        {
            return Result<PaymentWebhookEventPreparation>
                .Success(new PaymentWebhookEventPreparation(
                    webhookEvent.Id,
                    PaymentWebhookPreparationStatus.ReadyToProcess));
        }

        if (webhookEvent.ProcessingStatus == PaymentWebhookProcessingStatus.Processed)
        {
            return Result<PaymentWebhookEventPreparation>
                .Success(new PaymentWebhookEventPreparation(
                    webhookEvent.Id,
                    PaymentWebhookPreparationStatus.AlreadyProcessed));
        }

        if (webhookEvent.ProcessingStatus == PaymentWebhookProcessingStatus.Failed)
        {
            webhookEvent.BeginRetry();

            return Result<PaymentWebhookEventPreparation>
                .Success(new PaymentWebhookEventPreparation(
                    webhookEvent.Id,
                    PaymentWebhookPreparationStatus.ReadyToProcess));
        }

        if (webhookEvent.ProcessingStatus == PaymentWebhookProcessingStatus.Processing)
        {
            return Result<PaymentWebhookEventPreparation>
                .Failure(PaymentWebhookPersistenceErrors.EventAlreadyProcessing(eventId));
        }

        throw new InvalidOperationException(
            $"Unsupported webhook processing status '{webhookEvent.ProcessingStatus}'.");
    }

    public async Task MarkProcessedAsync(
        Guid eventRecordId,
        DateTimeOffset processedAtUtc,
        CancellationToken cancellationToken = default)
    {
        PaymentWebhookEvent webhookEvent = await GetRequiredTrackedAsync(eventRecordId, cancellationToken);

        webhookEvent.MarkProcessed(processedAtUtc);
    }

    public async Task MarkFailedAsync(
        Guid eventRecordId,
        Error error,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(error);

        PaymentWebhookEvent webhookEvent = await GetRequiredTrackedAsync(eventRecordId, cancellationToken);
        webhookEvent.MarkAsFailed(error.Code, error.Message);
    }

    public Task<StoredPaymentWebhookEvent?> GetAsync(
        string provider,
        string eventId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(provider);
        ArgumentException.ThrowIfNullOrWhiteSpace(eventId);

        return _dbContext
            .PaymentWebhookEvents
            .AsNoTracking()
            .Where(webhookEvent => webhookEvent.Provider == provider && webhookEvent.EventId == eventId)
            .Select(
                webhookEvent =>
                    new StoredPaymentWebhookEvent(
                        webhookEvent.Id,
                        webhookEvent.Provider,
                        webhookEvent.EventId,
                        webhookEvent.EventType,
                        webhookEvent.ReceivedAtUtc,
                        webhookEvent.ProcessedAtUtc,
                        webhookEvent.ProcessingStatus,
                        webhookEvent.ErrorCode,
                        webhookEvent.ErrorMessage))
            .SingleOrDefaultAsync(cancellationToken);
    }

    private async Task<PaymentWebhookEvent> GetRequiredTrackedAsync(
        Guid eventRecordId,
        CancellationToken cancellationToken)
    {
        PaymentWebhookEvent? webhookEvent = await _dbContext
            .PaymentWebhookEvents
            .FindAsync(new object[] { eventRecordId }, cancellationToken);

        return webhookEvent ?? throw new InvalidOperationException(
            $"Webhook event record '{eventRecordId}' was not found.");
    }
}
