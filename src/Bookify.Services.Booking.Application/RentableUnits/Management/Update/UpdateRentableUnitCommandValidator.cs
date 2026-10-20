using Bookify.Services.Booking.Application.Abstractions.Messaging;
using Bookify.Services.Booking.Domain.Shared;

namespace Bookify.Services.Booking.Application.RentableUnits.Management.Update;

public sealed class UpdateRentableUnitCommandValidator : IRequestValidator<UpdateRentableUnitCommand>
{
    public Result Validate(UpdateRentableUnitCommand request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.PropertyId == Guid.Empty)
        {
            return Result.Failure(RentableUnitManagementErrors.InvalidPropertyId);
        }

        return Result.Success();
    }
}
