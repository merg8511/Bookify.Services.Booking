namespace Bookify.Services.Booking.Application.Notifications;

public enum BookingNotificationType
{
    BookingApproved = 1,
    PaymentConfirmed = 2,
    BookingCancelled = 3,
    PaymentDeadlineReminder = 4,
    UpcomingCheckInReminder = 5
}
