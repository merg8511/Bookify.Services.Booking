namespace Bookify.Services.Booking.Application.Notifications;

public enum BookingNotificationDispatchOutcome
{
    Sent = 1,
    Skipped = 2,
    AlreadyDelivered = 3
}
