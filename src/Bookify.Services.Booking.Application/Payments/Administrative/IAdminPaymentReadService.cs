using Bookify.Services.Booking.Application.Payments.Administrative.ReadModels;

namespace Bookify.Services.Booking.Application.Payments.Administrative;

public interface IAdminPaymentReadService
{
    Task<AdminBookingPaymentReadModel?> GetByBookingAsync(
        Guid propertyId,
        Guid bookingId,
        CancellationToken cancellationToken = default);
}
