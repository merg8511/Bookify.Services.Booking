using Bookify.Services.Booking.Domain.Bookings;
using Bookify.Services.Booking.Domain.Shared;

namespace Bookify.Services.Booking.Application.Payments.Reconciliation;

public static class PaymentReconciliationErrors
{
    public static Error BookingStateConflict(
        Guid bookingId,
        BookingStatus status) =>
        Error.Failure(
            "Payments.Renciliation.BookingStateConflict",
            $"Booking '{bookingId}' cannot accept a succeeded " +
            $"payment while it is in '{status}' status.");
}
