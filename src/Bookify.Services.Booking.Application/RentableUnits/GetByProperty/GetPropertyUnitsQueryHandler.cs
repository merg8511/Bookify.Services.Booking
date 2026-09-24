using Bookify.Services.Booking.Application.Abstractions.Messaging;
using Bookify.Services.Booking.Application.Properties;
using Bookify.Services.Booking.Application.Properties.ReadModels;
using Bookify.Services.Booking.Application.RentableUnits.ReadModels;
using Bookify.Services.Booking.Domain.Shared;

namespace Bookify.Services.Booking.Application.RentableUnits.GetByProperty;

public sealed class GetPropertyUnitsQueryHandler :
    IQueryHandler<GetPropertyUnitsQuery, IReadOnlyList<RentableUnitListItemReadModel>>
{
    private readonly IPropertyReadService _propertyReadService;
    private readonly IRentableUnitReadService _rentableUnitReadService;

    public GetPropertyUnitsQueryHandler(
        IPropertyReadService propertyReadService,
        IRentableUnitReadService rentableUnitReadService)
    {
        _propertyReadService = propertyReadService ??
            throw new ArgumentNullException(nameof(propertyReadService));

        _rentableUnitReadService = rentableUnitReadService ??
            throw new ArgumentNullException(nameof(rentableUnitReadService));
    }

    public async Task<Result<IReadOnlyList<RentableUnitListItemReadModel>>> HandleAsync(
        GetPropertyUnitsQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        PropertyDetailsReadModel? property = await _propertyReadService
            .GetByIdAsync(query.PropertyId, cancellationToken);

        if (property is null)
        {
            return Result<IReadOnlyList<RentableUnitListItemReadModel>>.Failure(
                GetPropertyUnitsErrors.PropertyNotFound(query.PropertyId));
        }

        if (!property.IsActive)
        {
            return Result<IReadOnlyList<RentableUnitListItemReadModel>>.Failure(
                GetPropertyUnitsErrors.PropertyInactive(property.Id));
        }

        IReadOnlyList<RentableUnitListItemReadModel> units = await _rentableUnitReadService
            .GetActiveByPropertyIdAsync(property.Id, cancellationToken);

        return Result<IReadOnlyList<RentableUnitListItemReadModel>>.Success(units);
    }
}
