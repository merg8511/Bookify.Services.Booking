using DomainBooking = Bookify.Services.Booking.Domain.Bookings.Booking;

namespace Bookify.Services.Booking.Application.Abstractions.Persistence.Repositories;

public interface IBookingRepository
{
    Task<DomainBooking?> GetByIdAsync(
        Guid bookingId,
        CancellationToken cancellationToken = default);

    void Add(DomainBooking booking);
}
