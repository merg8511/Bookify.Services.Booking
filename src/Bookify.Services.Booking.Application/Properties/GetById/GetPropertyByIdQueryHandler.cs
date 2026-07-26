using Bookify.Services.Booking.Application.Abstractions.Messaging;
using Bookify.Services.Booking.Application.Properties.ReadModels;
using Bookify.Services.Booking.Domain.Shared;

namespace Bookify.Services.Booking.Application.Properties.GetById;

public sealed class GetPropertyByIdQueryHandler
    : IQueryHandler<GetPropertyByIdQuery, PropertyDetailsReadModel>
{
    private readonly IPropertyReadService _propertyReadService;

    public GetPropertyByIdQueryHandler(IPropertyReadService propertyReadService)
    {
        _propertyReadService = propertyReadService ??
            throw new ArgumentNullException(nameof(propertyReadService));
    }

    public async Task<Result<PropertyDetailsReadModel>> HandleAsync(
        GetPropertyByIdQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        PropertyDetailsReadModel? property =
            await _propertyReadService.GetByIdAsync(
                query.PropertyId,
                cancellationToken);

        if (property is null)
        {
            return Result<PropertyDetailsReadModel>.Failure(
                GetPropertyByIdErrors.NotFound(
                    query.PropertyId));
        }

        return Result<PropertyDetailsReadModel>.Success(property);
    }
}
