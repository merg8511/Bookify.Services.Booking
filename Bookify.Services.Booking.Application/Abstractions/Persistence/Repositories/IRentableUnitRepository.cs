using Bookify.Services.Booking.Domain.Properties;

namespace Bookify.Services.Booking.Application.Abstractions.Persistence.Repositories;

public interface IRentableUnitRepository
{
    Task<RentableUnit?> GetByIdAsync(
        Guid rentableUnitId,
        CancellationToken cancellationToken = default);

    void Add(RentableUnit rentableUnit);
}
