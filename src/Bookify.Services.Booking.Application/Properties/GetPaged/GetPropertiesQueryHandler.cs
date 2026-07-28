using Bookify.Services.Booking.Application.Abstractions.Messaging;
using Bookify.Services.Booking.Application.Common.Pagination;
using Bookify.Services.Booking.Application.Properties.ReadModels;
using Bookify.Services.Booking.Domain.Shared;

namespace Bookify.Services.Booking.Application.Properties.GetPaged;

internal sealed class GetPropertiesQueryHandler :
    IQueryHandler<
        GetPropertiesQuery,
        PagedResult<
            PropertyListItemReadModel>>
{
    private readonly IPropertyReadService _propertyReadService;

    public GetPropertiesQueryHandler(
        IPropertyReadService propertyReadService)
    {
        _propertyReadService = propertyReadService ??
                                throw new ArgumentNullException(nameof(propertyReadService));
    }

    public async Task<
       Result<
           PagedResult<
               PropertyListItemReadModel>>> HandleAsync(
       GetPropertiesQuery query,
       CancellationToken cancellationToken = default)
    {
        PagedResult<
            PropertyListItemReadModel> page =
            await _propertyReadService.GetPagedAsync(
                query.PageNumber,
                query.PageSize,
                cancellationToken);

        return Result<PagedResult<PropertyListItemReadModel>>.Success(page);
    }
}
