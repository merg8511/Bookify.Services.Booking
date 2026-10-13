using System.Diagnostics.Metrics;

namespace Bookify.Services.Booking.Application.Observability;

public static class BookingMetrics
{
    public const string MeterName = "Bookify.Services.Booking";
    private static readonly Meter Meter = new(MeterName);

    private static readonly Counter<long> BookingsCreated = Meter
        .CreateCounter<long>(
            "bookify.bookings.created",
            unit: "{booking}",
            description: "Number of successfully committed bookings.");

    private static readonly Counter<long> PaymentOutcomes = Meter
        .CreateCounter<long>(
            "bookify.payments.outcomes",
            unit: "{event}",
            description: "Payment outcomes reconciled from provider events.");

    private static readonly Counter<long> DuplicateWebhooks = Meter
        .CreateCounter<long>(
            "bookify.webhooks.duplicates",
            unit: "{event}",
            description: "Previously processed webhook events received again.");

    private static readonly Counter<long> JobsProcessed = Meter
        .CreateCounter<long>(
            "bookify.jobs.processed",
            unit: "{item}",
            description: "Number of items processed by background jobs.");

    private static readonly Counter<long> JobsFailed = Meter
        .CreateCounter<long>(
            "bookify.jobs.failed",
            unit: "{execution}",
            description: "Number of background job executions that failed.");

    private static readonly Counter<long> NotificationDeliveries = Meter
        .CreateCounter<long>(
            "bookify.notifications.delivery",
            unit: "{attempt}",
            description: "Notification delivery attempts and their outcomes.");

    public static void RecordBookingCreated()
    {
        BookingsCreated.Add(1);
    }

    public static void RecordPaymentOutcome(string status)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(status);

        if (status is not ("succeeded" or "failed"))
        {
            throw new ArgumentOutOfRangeException(nameof(status), "Unsupported payment outcome.");
        }

        PaymentOutcomes.Add(
            1,
            new KeyValuePair<string, object?>("payment.status", status));
    }

    public static void RecordDuplicateWebhook()
    {
        DuplicateWebhooks.Add(
            1,
            new KeyValuePair<string, object?>("webhook.provider", "stripe"));
    }

    public static void RecordJobProcessed(string jobName, int processedCount)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(jobName);

        if (processedCount <= 0)
        {
            return;
        }

        JobsProcessed.Add(
            processedCount,
            new KeyValuePair<string, object?>("job.name", jobName));
    }

    public static void RecordJobFailed(string jobName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(jobName);

        JobsFailed.Add(
            1,
            new KeyValuePair<string, object?>("job.name", jobName));
    }

    public static void RecordNotificationDelivery(string notificationType, string outcome)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(notificationType);

        if (outcome is not ("sent" or "failed"))
        {
            throw new ArgumentOutOfRangeException(
                nameof(outcome),
                "Unsupported notification outcome.");
        }

        NotificationDeliveries.Add(
            1,
            new KeyValuePair<string, object?>("notification.type", notificationType),
            new KeyValuePair<string, object?>("delivery.outcome", outcome));
    }
}
