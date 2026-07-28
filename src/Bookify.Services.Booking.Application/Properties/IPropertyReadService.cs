using Bookify.Services.Booking.Application.Common.Pagination;
using Bookify.Services.Booking.Application.Properties.ReadModels;

namespace Bookify.Services.Booking.Application.Properties;

public interface IPropertyReadService
{
    Task<PropertyDetailsReadModel?> GetByIdAsync(
        Guid propertyId,
        CancellationToken cancellationToken = default);

    Task<
        PagedResult<
            PropertyListItemReadModel>> GetPagedAsync(
                int pageNumber,
                int pageSize,
                string? name,
                bool? isActive,
                CancellationToken cancellationToken = default);
}
