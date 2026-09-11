using Bookify.Services.Booking.Application.Payments.ReadModels;

namespace Bookify.Services.Booking.Application.Payments;

public interface IPaymentReadService
{
    Task<PaymentStatusReadModel?> GetStatusByBookingIdAsync(
        Guid bookingId,
        CancellationToken cancellationToken = default);
}
