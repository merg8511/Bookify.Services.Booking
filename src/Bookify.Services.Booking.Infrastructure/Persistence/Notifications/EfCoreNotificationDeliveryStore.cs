using Bookify.Services.Booking.Application.Notifications;
using Microsoft.EntityFrameworkCore;

namespace Bookify.Services.Booking.Infrastructure.Persistence.Notifications;

internal sealed class EfCoreNotificationDeliveryStore : INotificationDeliveryStore
{
    private readonly BookingDbContext _dbContext;

    public EfCoreNotificationDeliveryStore(BookingDbContext dbContext)
    {
        _dbContext = dbContext ??
            throw new ArgumentNullException(nameof(dbContext));
    }


    public Task<bool> HasBeenDeliveredAsync(
        Guid bookingId,
        BookingNotificationType notificationType,
        CancellationToken cancellationToken = default)
    {
        return _dbContext.NotificationDeliveries
            .AsNoTracking()
            .AnyAsync(delivery => delivery.BookingId == bookingId &&
                delivery.NotificationType == notificationType,
                cancellationToken);
    }

    public async Task<bool> TryRecordDeliveredAsync(
        Guid bookingId,
        BookingNotificationType notificationType,
        DateTimeOffset deliveredAtUtc,
        CancellationToken cancellationToken = default)
    {
        DateTimeOffset normalizedDeliveredAtUtc = deliveredAtUtc.ToUniversalTime();
        string notificationTypeValue = notificationType.ToString();

        int affectedRows = await _dbContext.Database
            .ExecuteSqlInterpolatedAsync(
            $"""
            INSERT INTO notification_deliveries
            (
                booking_id,
                notification_type,
                delivered_at_utc
            )
            VALUES
            (
                {bookingId},
                {notificationTypeValue},
                {normalizedDeliveredAtUtc}
            )
            ON CONFLICT
            (
                booking_id,
                notification_type
            )
            DO NOTHING;
            """,
            cancellationToken);

        return affectedRows == 1;
    }
}
