namespace Bookify.Services.Booking.Application.Notifications;

public interface IBookingReminderSchedule
{
    TimeSpan PaymentDeadlineLeadTime { get; }
    TimeSpan UpcomingCheckInLeadTime { get; }
    TimeSpan SelectionWindow { get; }
}
