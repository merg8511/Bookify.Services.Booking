using Bookify.Services.Booking.Application.RentableUnits.ReadModels;

namespace Bookify.Services.Booking.Application.RentableUnits;

public interface IRentableUnitReadService
{
    Task<IReadOnlyList<RentableUnitListItemReadModel>> GetActiveByPropertyIdAsync(
        Guid propertyId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RentableUnitDetailsReadModel>> GetAllByPropertyIdAsync(
        Guid propertyId,
        CancellationToken cancellationToken = default);

    Task<RentableUnitDetailsReadModel?> GetAdminByIdAsync(
        Guid propertyId,
        Guid unitId,
        CancellationToken cancellationToken = default);

    Task<bool> HasAnyBookingsAsync(
        Guid unitId,
        CancellationToken cancellationToken = default);

    Task<int> GetMaximumBookedGuestCountAsync(
        Guid unitId,
        CancellationToken cancellationToken = default);
}
