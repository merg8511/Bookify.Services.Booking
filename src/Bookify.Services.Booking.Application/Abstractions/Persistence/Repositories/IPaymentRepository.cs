using Bookify.Services.Booking.Domain.Payments;

namespace Bookify.Services.Booking.Application.Abstractions.Persistence.Repositories;

public interface IPaymentRepository
{
    Task<Payment?> GetByBookingIdAsync(
        Guid bookingId,
        CancellationToken cancellationToken = default);

    void Add(Payment payment);
}
