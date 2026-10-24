using Bookify.Services.Booking.Application.Abstractions.Messaging;
using Bookify.Services.Booking.Application.Bookings.Administrative.ReadModels;
using Bookify.Services.Booking.Application.Common.Pagination;
using Bookify.Services.Booking.Application.Properties;
using Bookify.Services.Booking.Domain.Shared;

namespace Bookify.Services.Booking.Application.Bookings.Administrative.GetPaged;

public sealed class GetAdminBookingsQueryHandler :
    IQueryHandler<GetAdminBookingsQuery, PagedResult<AdminBookingListItemReadModel>>
{
    private readonly IPropertyReadService _propertyReadService;
    private readonly IAdminBookingReadService _adminBookingReadService;

    public GetAdminBookingsQueryHandler(
        IPropertyReadService propertyReadService,
        IAdminBookingReadService adminBookingReadService)
    {
        _propertyReadService = propertyReadService ??
            throw new ArgumentNullException(nameof(propertyReadService));

        _adminBookingReadService = adminBookingReadService ??
            throw new ArgumentNullException(nameof(adminBookingReadService));
    }

    public async Task<Result<PagedResult<AdminBookingListItemReadModel>>> HandleAsync(
        GetAdminBookingsQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var property = await _propertyReadService.GetByIdAsync(query.PropertyId, cancellationToken);

        if (property is null)
        {
            return Result<PagedResult<AdminBookingListItemReadModel>>.Failure(
                AdminBookingErrors.PropertyNotFound(query.PropertyId));
        }

        PagedResult<AdminBookingListItemReadModel> page = await _adminBookingReadService.GetPagedAsync(
            query.PropertyId,
            query.PageNumber,
            query.PageSize,
            query.Status,
            query.RangeStart,
            query.RangeEnd,
            query.UnitId,
            query.CustomerSubjectId,
            cancellationToken);

        return Result<PagedResult<AdminBookingListItemReadModel>>.Success(page);
    }
}
