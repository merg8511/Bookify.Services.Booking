using Microsoft.Extensions.Logging;

namespace Bookify.Services.Booking.Infrastructure.Observability;

internal static class InfrastructureLogEvents
{
    // 3000-3099 — Notifications / SMTP
    public static readonly EventId SmtpNotificationAccepted =
        new(3000, nameof(SmtpNotificationAccepted));

    public static readonly EventId SmtpDisconnectFailed =
        new(3001, nameof(SmtpDisconnectFailed));

    public static readonly EventId SmtpNotificationDeliveryFailed =
        new(3002, nameof(SmtpNotificationDeliveryFailed));

    // 3100-3199 — PostgreSQL coordination
    public static readonly EventId BackgroundJobClaimNotReleased =
        new(3010, nameof(BackgroundJobClaimNotReleased));

    public static readonly EventId BackgroundJobClaimReleaseFailed =
        new(3101, nameof(BackgroundJobClaimReleaseFailed));
}
