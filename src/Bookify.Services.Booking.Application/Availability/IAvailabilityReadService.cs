using Bookify.Services.Booking.Application.Availability.ReadModels;

namespace Bookify.Services.Booking.Application.Availability;

public interface IAvailabilityReadService
{
    Task<IReadOnlyList<
            OverlappingBookingReadModel>>
        GetInventoryConflictsAsync(
            Guid propertyId,
            Guid requestedRentableUnitId,
            DateOnly requestedCheckInDate,
            DateOnly requestedCheckOutDate,
            CancellationToken cancellationToken = default);

    Task<IReadOnlyList<
            AvailableRentableUnitReadModel>>
        GetAvailableUnitsAsync(
            Guid propertyId,
            DateOnly requestedCheckInDate,
            DateOnly requestedCheckOutDate,
            int guestCount,
            CancellationToken cancellationToken = default);
}
