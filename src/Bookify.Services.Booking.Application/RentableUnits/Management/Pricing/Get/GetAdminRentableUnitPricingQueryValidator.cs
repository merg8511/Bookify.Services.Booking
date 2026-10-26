using Bookify.Services.Booking.Application.Abstractions.Messaging;
using Bookify.Services.Booking.Domain.Shared;

namespace Bookify.Services.Booking.Application.RentableUnits.Management.Pricing.Get;

public sealed class GetAdminRentableUnitPricingQueryValidator :
    IRequestValidator<GetAdminRentableUnitPricingQuery>
{
    public Result Validate(GetAdminRentableUnitPricingQuery request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.PropertyId == Guid.Empty)
        {
            return Result.Failure(RentableUnitManagementErrors.InvalidPropertyId);
        }

        if (request.UnitId == Guid.Empty)
        {
            return Result.Failure(RentableUnitManagementErrors.InvalidUnitId);
        }

        return Result.Success();
    }
}
