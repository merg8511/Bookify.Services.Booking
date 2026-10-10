using Bookify.Services.Booking.Application.Notifications;

namespace Bookify.Services.Booking.Infrastructure.Notifications;

internal sealed class NotificationReminderOptions : IBookingReminderSchedule
{
    public const string SectionName = "Notifications:Reminders";
    public TimeSpan PaymentDeadlineLeadTime { get; set; } = TimeSpan.FromMinutes(10);

    public TimeSpan UpcomingCheckInLeadTime { get; set; } = TimeSpan.FromHours(24);

    public TimeSpan SelectionWindow { get; set; } = TimeSpan.FromSeconds(30);
}
