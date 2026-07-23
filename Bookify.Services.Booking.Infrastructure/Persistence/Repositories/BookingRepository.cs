using DomainBooking = Bookify.Services.Booking.Domain.Bookings.Booking;
using Bookify.Services.Booking.Application.Abstractions.Persistence.Repositories;

namespace Bookify.Services.Booking.Infrastructure.Persistence.Repositories;

internal sealed class BookingRepository
    : IBookingRepository
{
    private readonly BookingDbContext _dbContext;

    public BookingRepository(BookingDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<DomainBooking?> GetByIdAsync(
        Guid bookingId,
        CancellationToken cancellationToken = default)
    {
        object[] keyValues = [bookingId];

        return await _dbContext.Bookings.FindAsync(
            keyValues,
            cancellationToken);
    }

    public void Add(DomainBooking booking)
    {
        ArgumentNullException.ThrowIfNull(booking);

        _dbContext.Bookings.Add(booking);
    }
}
