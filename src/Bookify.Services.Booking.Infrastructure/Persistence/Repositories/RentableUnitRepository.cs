using Bookify.Services.Booking.Application.Abstractions.Persistence.Repositories;
using Bookify.Services.Booking.Domain.Properties;

namespace Bookify.Services.Booking.Infrastructure.Persistence.Repositories;

internal sealed class RentableUnitRepository
    : IRentableUnitRepository
{

    private readonly BookingDbContext _dbContext;

    public RentableUnitRepository(BookingDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<RentableUnit?> GetByIdAsync(
        Guid rentableUnitId,
        CancellationToken cancellationToken = default)
    {
        object[] keyValues = [rentableUnitId];

        return await _dbContext.RentableUnits.FindAsync(
            keyValues,
            cancellationToken);
    }

    public void Add(RentableUnit rentableUnit)
    {
        ArgumentNullException.ThrowIfNull(rentableUnit);

        _dbContext.RentableUnits.Add(rentableUnit);
    }
}
