using Bookify.Services.Booking.Application.Abstractions.Messaging;
using Bookify.Services.Booking.Domain.Shared;

namespace Bookify.Services.Booking.Application.Properties.GetById;

public sealed class GetPropertyByIdQueryValidator
    : IRequestValidator<GetPropertyByIdQuery>
{
    public Result Validate(GetPropertyByIdQuery request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.PropertyId == Guid.Empty)
        {
            return Result.Failure(
                GetPropertyByIdErrors.InvalidPropertyId);
        }

        return Result.Success();
    }
}
