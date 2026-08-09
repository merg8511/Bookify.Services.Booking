namespace Bookify.Services.Booking.Application.Bookings.Create;

public interface IBookingInventoryLock
{
    Task<bool> TryAcquireAsync(
        Guid propertyId,
        CancellationToken cancellationToken = default);
}
