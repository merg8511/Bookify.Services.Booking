using Bookify.Services.Booking.Application.Abstractions.Messaging;
using Bookify.Services.Booking.Application.Properties;
using Bookify.Services.Booking.Application.RentableUnits.ReadModels;
using Bookify.Services.Booking.Domain.Shared;

namespace Bookify.Services.Booking.Application.RentableUnits.Management.Pricing.Get;

public sealed class GetAdminRentableUnitPricingQueryHandler :
    IQueryHandler<GetAdminRentableUnitPricingQuery, AdminRentableUnitPricingReadModel>
{
    private readonly IPropertyReadService _propertyReadService;
    private readonly IRentableUnitReadService _unitReadService;
    private readonly IRentableUnitPricingReadService _pricingReadService;

    public GetAdminRentableUnitPricingQueryHandler(
        IPropertyReadService propertyReadService,
        IRentableUnitReadService unitReadService,
        IRentableUnitPricingReadService pricingReadService)
    {
        _propertyReadService = propertyReadService ??
            throw new ArgumentNullException(nameof(propertyReadService));

        _unitReadService = unitReadService ??
            throw new ArgumentNullException(nameof(unitReadService));

        _pricingReadService = pricingReadService ??
            throw new ArgumentNullException(nameof(pricingReadService));
    }

    public async Task<Result<AdminRentableUnitPricingReadModel>> HandleAsync(
        GetAdminRentableUnitPricingQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var property = await _propertyReadService.GetByIdAsync(
            query.PropertyId, cancellationToken);

        if (property is null)
        {
            return Result<AdminRentableUnitPricingReadModel>.Failure(
                RentableUnitManagementErrors.PropertyNotFound(query.PropertyId));
        }

        RentableUnitDetailsReadModel? unit = await _unitReadService
            .GetAdminByIdAsync(query.PropertyId, query.UnitId, cancellationToken);

        if (unit is null)
        {
            return Result<AdminRentableUnitPricingReadModel>.Failure(
                RentableUnitManagementErrors.UnitNotFound(query.UnitId));
        }

        RentableUnitPricingReadModel? pricing = await _pricingReadService
            .GetByUnitIdAsync(query.UnitId, cancellationToken);

        var response = new AdminRentableUnitPricingReadModel(
            query.PropertyId, query.UnitId, pricing);

        return Result<AdminRentableUnitPricingReadModel>.Success(response);
    }
}
