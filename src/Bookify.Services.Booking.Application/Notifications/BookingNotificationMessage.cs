namespace Bookify.Services.Booking.Application.Notifications;

public sealed record BookingNotificationMessage(
    Guid BookingId,
    string RecipientEmail,
    string Subject,
    string Body);
