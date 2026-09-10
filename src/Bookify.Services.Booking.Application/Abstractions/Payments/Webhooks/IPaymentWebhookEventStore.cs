using Bookify.Services.Booking.Domain.Shared;

namespace Bookify.Services.Booking.Application.Abstractions.Payments.Webhooks;

public interface IPaymentWebhookEventStore
{
    Task<Result<PaymentWebhookEventPreparation>> PrepareAsync(
        string provider,
        string eventId,
        string eventType,
        DateTimeOffset receivedAtUtc,
        CancellationToken cancellationToken = default);

    Task MarkProcessedAsync(
        Guid eventRecordId,
        DateTimeOffset processedAtUtc,
        CancellationToken cancellationToken = default);

    Task MarkFailedAsync(
        Guid eventRecordId,
        Error error,
        CancellationToken cancellationToken = default);

    Task<StoredPaymentWebhookEvent?> GetAsync(
        string provider,
        string eventId,
        CancellationToken cancellationToken = default);
}
