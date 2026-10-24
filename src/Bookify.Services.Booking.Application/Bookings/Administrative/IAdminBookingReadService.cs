using Bookify.Services.Booking.Application.Bookings.Administrative.ReadModels;
using Bookify.Services.Booking.Application.Common.Pagination;

namespace Bookify.Services.Booking.Application.Bookings.Administrative;

public interface IAdminBookingReadService
{
    Task<PagedResult<AdminBookingListItemReadModel>> GetPagedAsync(
        Guid propertyId,
        int pageNumber,
        int pageSize,
        string? status,
        DateOnly? rangeStart,
        DateOnly? rangeEnd,
        Guid? unitId,
        string? customerSubjectId,
        CancellationToken cancellationToken = default);
}
