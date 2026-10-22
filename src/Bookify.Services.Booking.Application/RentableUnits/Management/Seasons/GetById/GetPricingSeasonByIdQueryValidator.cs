using Bookify.Services.Booking.Application.Abstractions.Messaging;
using Bookify.Services.Booking.Domain.Shared;

namespace Bookify.Services.Booking.Application.RentableUnits.Management.Seasons.GetById;

public sealed class GetPricingSeasonByIdQueryValidator :
    IRequestValidator<GetPricingSeasonByIdQuery>
{
    public Result Validate(GetPricingSeasonByIdQuery request)
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

        if (request.SeasonId <= 0)
        {
            return Result.Failure(PricingSeasonManagementErrors.InvalidSeasonId);
        }

        return Result.Success();
    }
}
