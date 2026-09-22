namespace Bookify.Services.Booking.Application.Bookings.Policies;

public sealed class BookingDeadlinePolicy : IBookingDeadlinePolicy
{
    private readonly TimeSpan _approvalWindow;
    private readonly TimeSpan _paymentWindow;

    public BookingDeadlinePolicy(
        TimeSpan approvalWindow,
        TimeSpan paymentWindow)
    {
        if (approvalWindow <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(approvalWindow), "Approval window must be greater than zero.");
        }

        if (paymentWindow <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(paymentWindow), "Payment window must be greater than zero.");
        }

        _approvalWindow = approvalWindow;
        _paymentWindow = paymentWindow;
    }

    public DateTimeOffset GetApprovalDueAtUtc(DateTimeOffset createdAtUtc)
    {
        return createdAtUtc.Add(_approvalWindow);
    }

    public DateTimeOffset GetPaymentDueAtUtc(DateTimeOffset approvedAtUtc)
    {
        return approvedAtUtc.Add(_paymentWindow);
    }
}
