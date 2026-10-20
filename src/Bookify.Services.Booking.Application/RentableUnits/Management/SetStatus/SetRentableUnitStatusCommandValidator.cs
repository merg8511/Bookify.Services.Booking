using Bookify.Services.Booking.Application.Abstractions.Messaging;
using Bookify.Services.Booking.Domain.Shared;

namespace Bookify.Services.Booking.Application.RentableUnits.Management.SetStatus;

public sealed class SetRentableUnitStatusCommandValidator : IRequestValidator<SetRentableUnitStatusCommand>
{
    public Result Validate(SetRentableUnitStatusCommand request)
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
