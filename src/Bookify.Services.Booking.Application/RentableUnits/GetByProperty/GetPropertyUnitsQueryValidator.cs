using Bookify.Services.Booking.Application.Abstractions.Messaging;
using Bookify.Services.Booking.Domain.Shared;

namespace Bookify.Services.Booking.Application.RentableUnits.GetByProperty;

public sealed class GetPropertyUnitsQueryValidator : IRequestValidator<GetPropertyUnitsQuery>
{
    public Result Validate(GetPropertyUnitsQuery request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.PropertyId == Guid.Empty)
        {
            return Result.Failure(GetPropertyUnitsErrors.InvalidPropertyId);
        }

        return Result.Success();
    }
}
