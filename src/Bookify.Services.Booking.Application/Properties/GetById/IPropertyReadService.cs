using Bookify.Services.Booking.Application.Properties.ReadModels;

namespace Bookify.Services.Booking.Application.Properties.GetById;

public interface IPropertyReadService
{
    Task<PropertyDetailsReadModel?> GetByIdAsync(
        Guid propertyId,
        CancellationToken cancellationToken = default);
}
