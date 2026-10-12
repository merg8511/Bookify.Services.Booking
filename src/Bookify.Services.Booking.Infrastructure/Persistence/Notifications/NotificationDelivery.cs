using Bookify.Services.Booking.Application.Notifications;

namespace Bookify.Services.Booking.Infrastructure.Persistence.Notifications;

internal sealed class NotificationDelivery
{
    private NotificationDelivery()
    {

    }

    public Guid BookingId { get; private set; }
    public BookingNotificationType NotificationType { get; private set; }
    public DateTimeOffset DeliveredAtUtc { get; private set; }
}
