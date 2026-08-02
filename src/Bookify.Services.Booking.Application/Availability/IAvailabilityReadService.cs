using Bookify.Services.Booking.Application.Availability.ReadModels;

namespace Bookify.Services.Booking.Application.Availability;

public interface IAvailabilityReadService
{
    Task<IReadOnlyList<
            OverlappingBookingReadModel>>
        GetInventoryConflictCandidatesAsync(
            Guid propertyId,
            Guid requestedRentableUnitId,
            DateOnly requestedCheckInDate,
            DateOnly requestedCheckOutDate,
            CancellationToken cancellationToken = default);
}
