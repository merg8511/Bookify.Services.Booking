using Bookify.Services.Booking.Application.Abstractions.Payments.Webhooks;

namespace Bookify.Services.Booking.Infrastructure.Persistence.Payments.Webhooks;

internal sealed class PaymentWebhookEvent
{
    private PaymentWebhookEvent()
    {
    }

    public Guid Id { get; private set; }
    public string Provider { get; private set; } = string.Empty;
    public string EventId { get; private set; } = string.Empty;
    public string EventType { get; private set; } = string.Empty;
    public DateTimeOffset ReceivedAtUtc { get; private set; }
    public DateTimeOffset? ProcessedAtUtc { get; private set; }
    public PaymentWebhookProcessingStatus ProcessingStatus { get; private set; }
    public string? ErrorCode { get; private set; }
    public string? ErrorMessage { get; private set; }

    public static PaymentWebhookEvent Create(
        string provider,
        string eventId,
        string eventType,
        DateTimeOffset receivedAtUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(provider);
        ArgumentException.ThrowIfNullOrWhiteSpace(eventId);
        ArgumentException.ThrowIfNullOrWhiteSpace(eventType);

        return new PaymentWebhookEvent
        {
            Id = Guid.NewGuid(),
            Provider = provider,
            EventId = eventId,
            EventType = eventType,
            ReceivedAtUtc = receivedAtUtc,
            ProcessedAtUtc = null,
            ProcessingStatus = PaymentWebhookProcessingStatus.Processing,
            ErrorCode = null,
            ErrorMessage = null
        };
    }

    public void BeginRetry()
    {
        if (ProcessingStatus != PaymentWebhookProcessingStatus.Failed)
        {
            throw new InvalidOperationException("Only a failed webhook event can be retried.");
        }

        ProcessingStatus = PaymentWebhookProcessingStatus.Processing;
        ProcessedAtUtc = null;
        ErrorCode = null;
        ErrorMessage = null;
    }

    public void MarkProcessed(DateTimeOffset processedAtUtc)
    {
        if (ProcessingStatus != PaymentWebhookProcessingStatus.Processing)
        {
            throw new InvalidOperationException("Only a processing webhook event can be marked as processed.");
        }

        if (processedAtUtc < ReceivedAtUtc)
        {
            throw new InvalidOperationException("Only a processing webhook event can be market as processed.");
        }

        ProcessingStatus = PaymentWebhookProcessingStatus.Processed;
        ProcessedAtUtc = processedAtUtc;
        ErrorCode = null;
        ErrorMessage = null;
    }

    public void MarkAsFailed(string errorCode, string errorMessage)
    {
        if (ProcessingStatus != PaymentWebhookProcessingStatus.Processing)
        {
            throw new InvalidOperationException("Only a processing webhook event can be market as failed.");
        }

        ArgumentNullException.ThrowIfNullOrWhiteSpace(errorCode);
        ArgumentNullException.ThrowIfNullOrWhiteSpace(errorMessage);
        ProcessingStatus = PaymentWebhookProcessingStatus.Failed;
        ProcessedAtUtc = null;
        ErrorCode = errorCode;
        ErrorMessage = errorMessage;
    }
}
