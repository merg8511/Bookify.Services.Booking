using Bookify.Services.Booking.Application.Abstractions.Messaging;
using Bookify.Services.Booking.Domain.Shared;

namespace Bookify.Services.Booking.Application.Messaging;

internal static class RequestValidation
{
    public static Result Validate<TRequest>(
        TRequest request,
        IEnumerable<IRequestValidator<TRequest>> validators)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(validators);

        foreach (IRequestValidator<TRequest> validator in validators)
        {
            Result validationResult = validator.Validate(request);

            if (validationResult.IsFailure)
            {
                return validationResult;
            }
        }

        return Result.Success();
    }
}
