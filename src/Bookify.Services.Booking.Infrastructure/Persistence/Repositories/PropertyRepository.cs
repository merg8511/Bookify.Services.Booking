using Bookify.Services.Booking.Application.Abstractions.Persistence.Repositories;
using Bookify.Services.Booking.Domain.Properties;

namespace Bookify.Services.Booking.Infrastructure.Persistence.Repositories;

internal sealed class PropertyRepository
    : IPropertyRepository
{

    private readonly BookingDbContext _dbContext;

    public PropertyRepository(BookingDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Property?> GetByIdAsync(
        Guid propertyId,
        CancellationToken cancellationToken = default)
    {
        object[] keyValues = [propertyId];

        return await _dbContext.Properties.FindAsync(
            keyValues,
            cancellationToken);
    }

    public void Add(Property property)
    {
        ArgumentNullException.ThrowIfNull(property);

        _dbContext.Properties.Add(property);
    }
}
