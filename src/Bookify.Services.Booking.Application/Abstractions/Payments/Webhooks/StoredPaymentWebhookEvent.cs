namespace Bookify.Services.Booking.Application.Abstractions.Payments.Webhooks;

public sealed record StoredPaymentWebhookEvent(
    Guid Id,
    string Provider,
    string EventId,
    string EventType,
    DateTimeOffset ReceivedAtUtc,
    DateTimeOffset? ProcessedAtUtc,
    PaymentWebhookProcessingStatus ProcessingStatus,
    string? ErrorCode,
    string? ErrorMessage);
