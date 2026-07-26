namespace Bookify.Services.Booking.Domain.Bookings;

public enum BookingStatus
{
    PendingApproval = 1,
    PendingPayment = 2,
    Paid = 3,
    Completed = 4,
    Cancelled = 5
}
