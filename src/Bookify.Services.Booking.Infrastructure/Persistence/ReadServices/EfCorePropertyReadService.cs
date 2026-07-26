using Bookify.Services.Booking.Application.Properties.GetById;
using Bookify.Services.Booking.Application.Properties.ReadModels;
using Microsoft.EntityFrameworkCore;

namespace Bookify.Services.Booking.Infrastructure.Persistence.ReadServices;

internal sealed class EfCorePropertyReadService
    : IPropertyReadService
{
    private readonly BookingDbContext _dbContext;

    public EfCorePropertyReadService(BookingDbContext dbContext)
    {
        _dbContext = dbContext
            ??
            throw new ArgumentNullException(
                nameof(dbContext));
    }

    public async Task<PropertyDetailsReadModel?> GetByIdAsync(
        Guid propertyId,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.Properties
            .AsNoTracking()
            .Where(property => property.Id == propertyId)
            .Select(
                property =>
                    new PropertyDetailsReadModel
                    {
                        Id = property.Id,
                        Name = property.Name,
                        TimeZoneId = property.TimeZoneId,
                        CheckInTime = property.CheckInTime,
                        CheckOutTime = property.CheckOutTime,
                        IsActive = property.IsActive,
                    })
            .SingleOrDefaultAsync(cancellationToken);
    }
}
