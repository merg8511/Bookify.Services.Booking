namespace Bookify.Services.Booking.Domain.Payments;

public enum PaymentStatus
{
    Pending = 1,
    Succeeded = 2,
    Failed = 3,
    Cancelled = 4
}
