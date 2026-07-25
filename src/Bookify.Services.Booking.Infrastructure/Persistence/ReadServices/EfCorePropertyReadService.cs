using Bookify.Services.Booking.Application.Properties.GetById;
using Microsoft.EntityFrameworkCore;

namespace Bookify.Services.Booking.Infrastructure.Persistence.ReadServices;

internal sealed class EfCorePropertyReadService
    : IPropertyReadService
{
    private readonly BookingDbContext _dbContext;

    public EfCorePropertyReadService(BookingDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<PropertyResponse?> GetByIdAsync(
        Guid propertyId,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.Properties
            .AsNoTracking()
            .Where(
                property =>
                    property.Id == propertyId)
            .Select(
                property =>
                    new PropertyResponse(
                            property.Id,
                            property.Name,
                            property.TimeZoneId,
                            property.CheckInTime,
                            property.CheckOutTime,
                            property.IsActive))
            .SingleOrDefaultAsync(cancellationToken);
    }
}
