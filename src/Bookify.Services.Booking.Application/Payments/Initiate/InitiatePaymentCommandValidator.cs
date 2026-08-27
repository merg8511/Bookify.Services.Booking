using Bookify.Services.Booking.Application.Abstractions.Messaging;
using Bookify.Services.Booking.Domain.Shared;

namespace Bookify.Services.Booking.Application.Payments.Initiate;

public sealed class InitiatePaymentCommandValidator :
    IRequestValidator<InitiatePaymentCommand>
{
    public Result Validate(InitiatePaymentCommand request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.BookingId == Guid.Empty)
        {
            return Result.Failure(
                InitiatePaymentErrors.InvalidBookingId);
        }

        if (string.IsNullOrWhiteSpace(request.IdempotencyKey))
        {
            return Result.Failure(
                InitiatePaymentErrors.IdempotencyKeyRequired);
        }

        return Result.Success();
    }
}
