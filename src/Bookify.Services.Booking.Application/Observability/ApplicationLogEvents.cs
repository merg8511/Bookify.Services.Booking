using Microsoft.Extensions.Logging;

namespace Bookify.Services.Booking.Application.Observability;

internal static class ApplicationLogEvents
{
    // 1000-1099 — Request execution
    public static readonly EventId CommandExecutionStarted =
        new(1000, nameof(CommandExecutionStarted));

    public static readonly EventId CommandExecutionCompleted =
        new(1001, nameof(CommandExecutionCompleted));

    public static readonly EventId QueryExecutionCompleted =
        new(1002, nameof(QueryExecutionCompleted));

    // 1100-1199 — Booking lifecycle background jobs
    public static readonly EventId ApprovalExpirationFailed =
        new(1100, nameof(ApprovalExpirationFailed));

    public static readonly EventId ApprovalExpirationUnhandledException =
        new(1101, nameof(ApprovalExpirationUnhandledException));

    public static readonly EventId PaymentExpirationFailed =
        new(1110, nameof(PaymentExpirationFailed));

    public static readonly EventId PaymentExpirationUnhandledException =
        new(1111, nameof(PaymentExpirationUnhandledException));

    public static readonly EventId BookingCompletionFailed =
        new(1120, nameof(BookingCompletionFailed));

    public static readonly EventId BookingCompletionUnhandledException =
        new(1121, nameof(BookingCompletionUnhandledException));

    // 1200-1299 — Notification background jobs
    public static readonly EventId OutboxMessageUnhandledException =
        new(1200, nameof(OutboxMessageUnhandledException));

    public static readonly EventId OutboxUnsupportedEventType =
        new(1201, nameof(OutboxUnsupportedEventType));

    public static readonly EventId OutboxInvalidPayload =
        new(1202, nameof(OutboxInvalidPayload));

    public static readonly EventId OutboxNotificationDeliveryFailed =
        new(1203, nameof(OutboxNotificationDeliveryFailed));

    public static readonly EventId PaymentDeadlineReminderFailed =
        new(1210, nameof(PaymentDeadlineReminderFailed));

    public static readonly EventId PaymentdeadlineReminderUnhandledException =
        new(1211, nameof(PaymentdeadlineReminderUnhandledException));

    public static readonly EventId UpcomingCheckInReminderFailed =
        new(1220, nameof(UpcomingCheckInReminderFailed));

    public static readonly EventId UpcomingCheckInReminderUnhandledException =
        new(1221, nameof(UpcomingCheckInReminderUnhandledException));

}
