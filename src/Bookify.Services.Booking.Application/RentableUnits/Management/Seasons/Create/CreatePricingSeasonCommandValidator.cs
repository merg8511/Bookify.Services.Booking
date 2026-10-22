using Bookify.Services.Booking.Application.Abstractions.Messaging;
using Bookify.Services.Booking.Domain.Shared;

namespace Bookify.Services.Booking.Application.RentableUnits.Management.Seasons.Create;

public sealed class CreatePricingSeasonCommandValidator : IRequestValidator<CreatePricingSeasonCommand>
{
    public Result Validate(CreatePricingSeasonCommand request)
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
