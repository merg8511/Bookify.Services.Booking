using Bookify.Services.Booking.Application.Properties.GetById;
using Bookify.Services.Booking.Domain.Properties;

namespace Bookify.Services.Booking.Infrastructure.Persistence.InMemory;

internal sealed class InMemoryPropertyReadService : IPropertyReadService
{
    private readonly InMemoryBookingStore _store;

    public InMemoryPropertyReadService(InMemoryBookingStore store)
    {
        _store = store;
    }

    public Task<PropertyResponse?> GetByIdAsync(
        Guid propertyId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        Property? property = _store.GetPropertyById(propertyId);

        PropertyResponse? response =
            property is null
                ? null
                : new PropertyResponse(
                    property.Id,
                    property.Name,
                    property.TimeZoneId,
                    property.CheckInTime,
                    property.CheckOutTime,
                    property.IsActive);

        return Task.FromResult(response);
    }
}
