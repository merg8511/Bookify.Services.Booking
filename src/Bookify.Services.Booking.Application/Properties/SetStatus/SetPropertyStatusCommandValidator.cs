using Bookify.Services.Booking.Application.Abstractions.Messaging;
using Bookify.Services.Booking.Application.Properties.Management;
using Bookify.Services.Booking.Domain.Shared;

namespace Bookify.Services.Booking.Application.Properties.SetStatus;

public sealed class SetPropertyStatusCommandValidator : IRequestValidator<SetPropertyStatusCommand>
{
    public Result Validate(SetPropertyStatusCommand request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.PropertyId == Guid.Empty)
        {
            return Result.Failure(PropertyManagementErrors.InvalidPropertyId);
        }

        return Result.Success();
    }
}
