namespace Bookify.Services.Booking.Application.Properties.GetById;

public interface IPropertyReadService
{
    Task<PropertyResponse?> GetByIdAsync(
        Guid propertyId,
        CancellationToken cancellationToken = default);
}
