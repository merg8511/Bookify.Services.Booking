namespace Bookify.Services.Booking.Application.Bookings.Create;

public interface IBookingAvailabilityReader
{
    Task<bool> HasConflictAsync(
        Guid propertyId,
        Guid requestedRentableUnitId,
        DateOnly requestedCheckInDate,
        DateOnly requestedCheckOutDate,
        CancellationToken cancellationToken = default);
}
