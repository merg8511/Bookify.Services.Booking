namespace Bookify.Services.Booking.Application.Abstractions.BackgroundJobs;

public static class BackgroundJobClaimKeys
{
    public const string ApprovalExpiration = "approval-expiration";
    public const string PaymentExpiration = "payment-expiration";
    public const string BookingCompletion = "booking-completion";
    public const string OutboxNotification = "outbox-notification";
    public const string PaymentDeadlineReminder = "payment-deadline-reminder";
    public const string UpcomingCheckInReminder = "upcoming-check-in-reminder";
}
