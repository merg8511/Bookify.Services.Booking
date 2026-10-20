using Bookify.Services.Booking.Application.Abstractions.Messaging;
using Bookify.Services.Booking.Domain.Shared;

namespace Bookify.Services.Booking.Application.RentableUnits.Management.GetAll;

public sealed class GetAdminRentableUnitsQueryValidator : IRequestValidator<GetAdminRentableUnitsQuery>
{
    public Result Validate(GetAdminRentableUnitsQuery request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.PropertyId == Guid.Empty)
        {
            return Result.Failure(RentableUnitManagementErrors.InvalidPropertyId);
        }

        return Result.Success();
    }
}
