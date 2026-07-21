using Bookify.Services.Booking.Application.Abstractions.Persistence.Repositories;
using Bookify.Services.Booking.Domain.Properties;

namespace Bookify.Services.Booking.Infrastructure.Persistence.InMemory;

internal sealed class InMemoryPropertyRepository : IPropertyRepository
{
    private readonly InMemoryBookingStore _store;
    private readonly InMemoryUnitOfWork _unitOfWork;
    public InMemoryPropertyRepository(InMemoryUnitOfWork unitOfWort, InMemoryBookingStore store)
    {
        _unitOfWork = unitOfWort;
        _store = store;
    }

    public void Add(Property property)
    {
        ArgumentNullException.ThrowIfNull(property);

        _unitOfWork.Stage(property);
    }

    public Task<Property?> GetByIdAsync(Guid propertyId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        Property? property = _store.GetPropertyById(propertyId);

        return Task.FromResult(property);
    }
}
