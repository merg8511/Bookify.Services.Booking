using Bookify.Services.Booking.Application.Bookings.Policies;

namespace Bookify.Services.Booking.Application.Tests.Infrastructure;

internal static class BookingTestDeadlinePolicy
{
    public static readonly TimeSpan ApprovalWindow =
        TimeSpan.FromHours(24);

    public static readonly TimeSpan PaymentWindow =
        TimeSpan.FromMinutes(30);

    public static IBookingDeadlinePolicy Create()
    {
        return new BookingDeadlinePolicy(
            ApprovalWindow,
            PaymentWindow);
    }
}
