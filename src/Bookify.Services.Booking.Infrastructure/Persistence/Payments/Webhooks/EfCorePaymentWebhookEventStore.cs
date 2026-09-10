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

        PaymentWebhookEvent? existingEvent = await _dbContext
            .PaymentWebhookEvents
            .SingleOrDefaultAsync(webhookEvent => webhookEvent.EventId == eventId, cancellationToken);

        if (existingEvent is null)
        {
            PaymentWebhookEvent webhookEvent = PaymentWebhookEvent.Create(
                provider,
                eventId,
                eventType,
                receivedAtUtc);

            _dbContext.PaymentWebhookEvents.Add(webhookEvent);

            return Result<PaymentWebhookEventPreparation>
                .Success(new PaymentWebhookEventPreparation(
                    webhookEvent.Id,
                    PaymentWebhookPreparationStatus.ReadyToProcess));
        }

        bool sameIdentity =
            string.Equals(existingEvent.Provider, provider, StringComparison.Ordinal) &&
            string.Equals(existingEvent.EventType, eventType, StringComparison.Ordinal);

        if (!sameIdentity)
        {
            return Result<PaymentWebhookEventPreparation>
                .Failure(PaymentWebhookPersistenceErrors.EventIdentityMismatch(eventId));
        }

        if (existingEvent.ProcessingStatus == PaymentWebhookProcessingStatus.Processed)
        {
            return Result<PaymentWebhookEventPreparation>
                .Success(new PaymentWebhookEventPreparation(
                    existingEvent.Id,
                    PaymentWebhookPreparationStatus.AlreadyProcessed));
        }

        if (existingEvent.ProcessingStatus == PaymentWebhookProcessingStatus.Failed)
        {
            existingEvent.BeginRetry();

            return Result<PaymentWebhookEventPreparation>
                .Success(new PaymentWebhookEventPreparation(
                    existingEvent.Id,
                    PaymentWebhookPreparationStatus.ReadyToProcess));
        }

        if (existingEvent.ProcessingStatus == PaymentWebhookProcessingStatus.Processing)
        {
            return Result<PaymentWebhookEventPreparation>
                .Failure(PaymentWebhookPersistenceErrors.EventAlreadyProcessing(eventId));
        }

        throw new InvalidOperationException(
            $"Unsupported webhook processing status " +
            $"'{existingEvent.ProcessingStatus}'.");
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
