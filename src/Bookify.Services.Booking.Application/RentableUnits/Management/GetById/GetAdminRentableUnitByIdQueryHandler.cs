using Bookify.Services.Booking.Application.Abstractions.Messaging;
using Bookify.Services.Booking.Application.Properties;
using Bookify.Services.Booking.Application.RentableUnits.ReadModels;
using Bookify.Services.Booking.Domain.Shared;

namespace Bookify.Services.Booking.Application.RentableUnits.Management.GetById;

public sealed class GetAdminRentableUnitByIdQueryHandler :
    IQueryHandler<GetAdminRentableUnitByIdQuery, RentableUnitDetailsReadModel>
{
    private readonly IPropertyReadService _propertyReadService;
    private readonly IRentableUnitReadService _unitReadService;

    public GetAdminRentableUnitByIdQueryHandler(
        IPropertyReadService propertyReadService,
        IRentableUnitReadService unitReadService)
    {
        _propertyReadService = propertyReadService;
        _unitReadService = unitReadService;
    }

    public async Task<Result<RentableUnitDetailsReadModel>> HandleAsync(
        GetAdminRentableUnitByIdQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var property = await _propertyReadService.GetByIdAsync(query.PropertyId, cancellationToken);

        if (property is null)
        {
            return Result<RentableUnitDetailsReadModel>.Failure(
                RentableUnitManagementErrors.PropertyNotFound(query.PropertyId));
        }

        RentableUnitDetailsReadModel? unit = await _unitReadService.GetAdminByIdAsync(
            query.PropertyId, query.UnitId, cancellationToken);

        if (unit is null)
        {
            return Result<RentableUnitDetailsReadModel>.Failure(
                RentableUnitManagementErrors.UnitNotFound(query.UnitId));
        }

        return Result<RentableUnitDetailsReadModel>.Success(unit);
    }
}
