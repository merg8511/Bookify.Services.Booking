namespace Bookify.Services.Booking.Application.Bookings.Policies;

public interface IBookingDeadlinePolicy
{
    DateTimeOffset GetApprovalDueAtUtc(DateTimeOffset createdAtUtc);
    DateTimeOffset GetPaymentDueAtUtc(DateTimeOffset approvedAtUtc);
}
