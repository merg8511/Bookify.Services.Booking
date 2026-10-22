using Bookify.Services.Booking.Application.Abstractions.Messaging;
using Bookify.Services.Booking.Application.Properties;
using Bookify.Services.Booking.Domain.Shared;

namespace Bookify.Services.Booking.Application.RentableUnits.Management.Seasons.GetAll;

public sealed class GetPricingSeasonsQueryHandler :
    IQueryHandler<GetPricingSeasonsQuery, IReadOnlyList<PricingSeasonReadModel>>
{
    private readonly IPropertyReadService _propertyReadService;
    private readonly IRentableUnitReadService _unitReadService;
    private readonly IPricingSeasonReadService _seasonReadService;

    public GetPricingSeasonsQueryHandler(
        IPropertyReadService propertyReadService,
        IRentableUnitReadService unitReadService,
        IPricingSeasonReadService seasonReadService)
    {
        _propertyReadService = propertyReadService;
        _unitReadService = unitReadService;
        _seasonReadService = seasonReadService;
    }

    public async Task<Result<IReadOnlyList<PricingSeasonReadModel>>> HandleAsync(
        GetPricingSeasonsQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var property = await _propertyReadService.GetByIdAsync(query.PropertyId, cancellationToken);

        if (property is null)
        {
            return Result<IReadOnlyList<PricingSeasonReadModel>>.Failure(
                RentableUnitManagementErrors.PropertyNotFound(query.PropertyId));
        }

        var unit = await _unitReadService.GetAdminByIdAsync(
            query.PropertyId, query.UnitId, cancellationToken);

        if (unit is null)
        {
            return Result<IReadOnlyList<PricingSeasonReadModel>>.Failure(
                RentableUnitManagementErrors.UnitNotFound(query.UnitId));
        }

        IReadOnlyList<PricingSeasonReadModel> seasons = await _seasonReadService.GetAllAsync(
            query.UnitId, cancellationToken);

        return Result<IReadOnlyList<PricingSeasonReadModel>>.Success(seasons);
    }
}
