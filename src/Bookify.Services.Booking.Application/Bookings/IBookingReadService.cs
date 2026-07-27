using Bookify.Services.Booking.Application.Bookings.ReadModels;

namespace Bookify.Services.Booking.Application.Bookings;

public interface IBookingReadService
{
    Task<BookingDetailsReadModel?> GetByIdAsync(
        Guid bookingId,
        CancellationToken cancellationToken = default);

    Task<
        IReadOnlyList<
            BookingCalendarItemReadModel>>
        GetCalendarAsync(
            Guid propertyId,
            DateOnly rangeStart,
            DateOnly rangeEnd,
            CancellationToken cancellationToken = default);
}
