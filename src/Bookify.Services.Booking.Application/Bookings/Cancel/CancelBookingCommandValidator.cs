using Bookify.Services.Booking.Application.Abstractions.Messaging;
using Bookify.Services.Booking.Domain.Shared;

namespace Bookify.Services.Booking.Application.Bookings.Cancel;

public sealed class CancelBookingCommandValidator
    : IRequestValidator<CancelBookingCommand>
{
    public Result Validate(CancelBookingCommand request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.BookingId == Guid.Empty)
        {
            return Result.Failure(
                CancelBookingErrors.InvalidBookingId);
        }

        return Result.Success();
    }
}
