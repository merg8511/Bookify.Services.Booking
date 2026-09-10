namespace Bookify.Services.Booking.Application.Abstractions.Payments.Webhooks;

public sealed record PaymentWebhookEventPreparation(
    Guid EventRecordId,
    PaymentWebhookPreparationStatus Status);
