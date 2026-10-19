namespace Bookify.Services.Booking.Application.Properties;

public interface IPropertyOwnershipReadService
{
    Task<string?> GetOwnerSubjectIdAsync(
        Guid propertyId,
        CancellationToken cancellationToken = default);
}
