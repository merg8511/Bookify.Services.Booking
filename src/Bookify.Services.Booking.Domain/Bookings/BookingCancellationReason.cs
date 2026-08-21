namespace Bookify.Services.Booking.Domain.Bookings;

public enum BookingCancellationReason
{
    RejectedByOwner = 1,
    PaymentExpired = 2,
    CancelledByGuest = 3
}
