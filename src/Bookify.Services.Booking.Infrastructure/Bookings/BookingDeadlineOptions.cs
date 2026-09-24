namespace Bookify.Services.Booking.Infrastructure.Bookings;

internal sealed class BookingDeadlineOptions
{
    public const string SectionName = "BookingDeadlines";

    public TimeSpan ApprovalWindow { get; init; }
    public TimeSpan PaymentWindow { get; init; }
}
