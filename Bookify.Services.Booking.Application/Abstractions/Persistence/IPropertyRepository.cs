using Bookify.Services.Booking.Domain.Properties;

namespace Bookify.Services.Booking.Application.Abstractions.Persistence;

public interface IPropertyRepository
{
    void Add(Property property);
}
