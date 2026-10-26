using Bookify.Services.Booking.Application.Abstractions.Messaging;
using Bookify.Services.Booking.Application.Properties.Management;
using Bookify.Services.Booking.Domain.Shared;

namespace Bookify.Services.Booking.Application.Properties.Update;

public sealed class UpdatePropertyCommandValidator : IRequestValidator<UpdatePropertyCommand>
{
    public Result Validate(UpdatePropertyCommand request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.PropertyId == Guid.Empty)
        {
            return Result.Failure(PropertyManagementErrors.InvalidPropertyId);
        }

        return Result.Success();
    }
}
