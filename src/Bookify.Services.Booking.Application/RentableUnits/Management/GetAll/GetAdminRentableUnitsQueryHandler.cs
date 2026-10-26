using Bookify.Services.Booking.Application.Abstractions.Messaging;
using Bookify.Services.Booking.Application.Properties;
using Bookify.Services.Booking.Application.RentableUnits.ReadModels;
using Bookify.Services.Booking.Domain.Shared;

namespace Bookify.Services.Booking.Application.RentableUnits.Management.GetAll;

public sealed class GetAdminRentableUnitsQueryHandler
    : IQueryHandler<GetAdminRentableUnitsQuery, IReadOnlyList<RentableUnitDetailsReadModel>>
{
    private readonly IPropertyReadService _propertyReadService;
    private readonly IRentableUnitReadService _unitReadService;

    public GetAdminRentableUnitsQueryHandler(
        IPropertyReadService propertyReadService,
        IRentableUnitReadService unitReadService)
    {
        _propertyReadService = propertyReadService;
        _unitReadService = unitReadService;
    }

    public async Task<Result<IReadOnlyList<RentableUnitDetailsReadModel>>> HandleAsync(
        GetAdminRentableUnitsQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var property = await _propertyReadService.GetByIdAsync(query.PropertyId, cancellationToken);

        if (property is null)
        {
            return Result<IReadOnlyList<RentableUnitDetailsReadModel>>.Failure(
                RentableUnitManagementErrors.PropertyNotFound(query.PropertyId));
        }

        IReadOnlyList<RentableUnitDetailsReadModel> units = await _unitReadService
            .GetAllByPropertyIdAsync(query.PropertyId, cancellationToken);

        return Result<IReadOnlyList<RentableUnitDetailsReadModel>>.Success(units);
    }
}
