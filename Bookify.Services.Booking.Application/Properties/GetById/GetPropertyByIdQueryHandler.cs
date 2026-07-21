using Bookify.Services.Booking.Application.Abstractions.Messaging;
using Bookify.Services.Booking.Domain.Shared;

namespace Bookify.Services.Booking.Application.Properties.GetById;

public sealed class GetPropertyByIdQueryHandler
    : IQueryHandler<GetPropertyByIdQuery, PropertyResponse>
{
    private readonly IPropertyReadService _propertyReadService;

    public GetPropertyByIdQueryHandler(IPropertyReadService propertyReadService)
    {
        _propertyReadService = propertyReadService;
    }
    public async Task<Result<PropertyResponse>> HandleAsync(
        GetPropertyByIdQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (query.PropertyId == Guid.Empty)
        {
            return Result<PropertyResponse>.Failure(
                GetPropertyByIdErrors.InvalidPropertyId);
        }

        PropertyResponse? property =
            await _propertyReadService.GetByIdAsync(
                query.PropertyId,
                cancellationToken);

        if (property is null)
        {
            return Result<PropertyResponse>.Failure(
                GetPropertyByIdErrors.NotFound(
                    query.PropertyId));
        }

        return Result<PropertyResponse>.Success(property);
    }
}
