using Bookify.Services.Booking.Application.Abstractions.Messaging;
using Bookify.Services.Booking.Domain.Shared;

namespace Bookify.Services.Booking.Application.RentableUnits.Management.GetById;

public sealed class GetAdminRentableUnitByIdQueryValidator :
    IRequestValidator<GetAdminRentableUnitByIdQuery>
{
    public Result Validate(GetAdminRentableUnitByIdQuery request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.PropertyId == Guid.Empty)
        {
            return Result.Failure(RentableUnitManagementErrors.InvalidPropertyId);
        }

        if (request.UnitId == Guid.Empty)
        {
            return Result.Failure(RentableUnitManagementErrors.InvalidPropertyId);
        }

        return Result.Success();
    }
}
