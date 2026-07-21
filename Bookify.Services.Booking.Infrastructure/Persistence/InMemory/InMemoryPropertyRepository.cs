using Bookify.Services.Booking.Application.Abstractions.Persistence;
using Bookify.Services.Booking.Domain.Properties;

namespace Bookify.Services.Booking.Infrastructure.Persistence.InMemory;

internal sealed class InMemoryPropertyRepository : IPropertyRepository
{
    private readonly InMemoryUnitOfWork _unitOfWork;
    public InMemoryPropertyRepository(InMemoryUnitOfWork unitOfWort)
    {
        _unitOfWork = unitOfWort;
    }

    public void Add(Property property)
    {
        ArgumentNullException.ThrowIfNull(property);

        _unitOfWork.Stage(property);
    }

}
