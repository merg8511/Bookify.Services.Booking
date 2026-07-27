using Bookify.Services.Booking.Application.RentableUnits.ReadModels;

namespace Bookify.Services.Booking.Application.RentableUnits;

public interface IRentableUnitReadService
{
    Task<IReadOnlyList<
        RentableUnitListItemReadModel>>
        GetByPropertyIdAsync(
        Guid propertyId,
        CancellationToken cancellationToken = default);
}
