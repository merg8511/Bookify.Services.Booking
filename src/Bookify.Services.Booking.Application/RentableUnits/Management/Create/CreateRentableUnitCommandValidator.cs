using Bookify.Services.Booking.Application.Abstractions.Messaging;
using Bookify.Services.Booking.Domain.Shared;

namespace Bookify.Services.Booking.Application.RentableUnits.Management.Create;

public sealed class CreateRentableUnitCommandValidator : IRequestValidator<CreateRentableUnitCommand>
{
    public Result Validate(CreateRentableUnitCommand request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.PropertyId == Guid.Empty)
        {
            return Result.Failure(RentableUnitManagementErrors.InvalidPropertyId);
        }

        return Result.Success();
    }
}
