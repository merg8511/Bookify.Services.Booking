using Bookify.Services.Booking.Domain.Properties;

namespace Bookify.Services.Booking.Application.Abstractions.Persistence.Repositories;

public interface IPropertyRepository
{
    Task<Property?> GetByIdAsync(
        Guid propertyId,
        CancellationToken cancellationToken = default);
    void Add(Property property);
}
