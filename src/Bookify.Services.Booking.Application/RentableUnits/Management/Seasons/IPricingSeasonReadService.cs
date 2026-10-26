using Bookify.Services.Booking.Application.Availability.ReadModels;

namespace Bookify.Services.Booking.Application.RentableUnits.Management.Seasons;

public interface IPricingSeasonReadService
{
    Task<IReadOnlyList<PricingSeasonReadModel>> GetAllAsync(
        Guid unitId,
        CancellationToken cancellationToken = default);

    Task<PricingSeasonReadModel?> GetByIdAsync(
        Guid unitId,
        int seasonId,
        CancellationToken cancellationToken = default);
}
