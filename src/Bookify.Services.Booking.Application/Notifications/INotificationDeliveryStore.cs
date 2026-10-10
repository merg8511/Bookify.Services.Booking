namespace Bookify.Services.Booking.Application.Notifications;

public interface INotificationDeliveryStore
{
    Task<bool> HasBeenDeliveredAsync(
        Guid bookingId,
        BookingNotificationType notificationType,
        CancellationToken cancellationToken = default);

    Task<bool> TryRecordDeliveredAsync(
        Guid bookingId,
        BookingNotificationType notificationType,
        DateTimeOffset deliveredAtUtc,
        CancellationToken cancellationToken = default);
}
