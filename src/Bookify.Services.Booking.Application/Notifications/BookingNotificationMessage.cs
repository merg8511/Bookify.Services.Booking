namespace Bookify.Services.Booking.Application.Notifications;

public sealed record BookingNotificationMessage(
    Guid BookingId,
    BookingNotificationType Type,
    string RecipientEmail,
    string Subject,
    string Body);
