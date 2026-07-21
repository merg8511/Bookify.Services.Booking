using Bookify.Services.Booking.Domain.Properties;

namespace Bookify.Services.Booking.Infrastructure.Persistence.InMemory;

internal sealed class InMemoryBookingStore
{
    private readonly object _syncRoot = new();
    private readonly Dictionary<Guid, Property> _properties = [];

    public void CommitProperties(
        IReadOnlyCollection<Property> properties)
    {
        ArgumentNullException.ThrowIfNull(properties);

        lock (_syncRoot)
        {
            EnsurePropertiesDoNotExist(properties);

            foreach (Property property in properties)
            {
                _properties.Add(
                    property.Id,
                    property);
            }
        }
    }

    private void EnsurePropertiesDoNotExist(
        IReadOnlyCollection<Property> properties)
    {
        foreach (Property property in properties)
        {
            if (_properties.ContainsKey(property.Id))
            {
                throw new InvalidOperationException(
                    $"A property with ID '{property.Id}' already exists.");
            }
        }
    }
}
