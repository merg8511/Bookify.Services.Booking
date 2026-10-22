using Bookify.Services.Booking.Application.Abstractions.Messaging;
using Bookify.Services.Booking.Application.Properties;
using Bookify.Services.Booking.Domain.Bookings.Errors;
using Bookify.Services.Booking.Domain.Shared;

namespace Bookify.Services.Booking.Application.RentableUnits.Management.Seasons.GetById;

public sealed class GetPricingSeasonByIdQueryHandler :
    IQueryHandler<GetPricingSeasonByIdQuery, PricingSeasonReadModel>
{
    private readonly IPropertyReadService _propertyReadService;
    private readonly IRentableUnitReadService _unitReadService;
    private readonly IPricingSeasonReadService _seasonReadService;

    public GetPricingSeasonByIdQueryHandler(
        IPropertyReadService propertyReadService,
        IRentableUnitReadService unitReadService,
        IPricingSeasonReadService seasonReadService)
    {
        _propertyReadService = propertyReadService;
        _unitReadService = unitReadService;
        _seasonReadService = seasonReadService;
    }

    public async Task<Result<PricingSeasonReadModel>> HandleAsync(
        GetPricingSeasonByIdQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var property = await _propertyReadService.GetByIdAsync(query.PropertyId, cancellationToken);

        if (property is null)
        {
            return Result<PricingSeasonReadModel>.Failure(
                RentableUnitManagementErrors.PropertyNotFound(query.PropertyId));
        }

        var unit = await _unitReadService.GetAdminByIdAsync(
            query.PropertyId, query.UnitId, cancellationToken);

        if (unit is null)
        {
            return Result<PricingSeasonReadModel>.Failure(
                RentableUnitManagementErrors.UnitNotFound(query.UnitId));
        }

        PricingSeasonReadModel? season = await _seasonReadService.GetByIdAsync(
            query.UnitId, query.SeasonId, cancellationToken);

        if (season is null)
        {
            return Result<PricingSeasonReadModel>.Failure(
                PricingSeasonErrors.NotFound(query.SeasonId));
        }

        return Result<PricingSeasonReadModel>.Success(season);
    }
}
