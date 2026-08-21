using Bookify.Services.Booking.Application.Abstractions.Messaging;
using Bookify.Services.Booking.Domain.Shared;

namespace Bookify.Services.Booking.Application.Bookings.Complete;

public sealed class CompleteBookingCommandValidator
    : IRequestValidator<CompleteBookingCommand>
{
    public Result Validate(CompleteBookingCommand request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.BookingId == Guid.Empty)
        {
            return Result.Failure(
                CompleteBookingErrors.InvalidBookingId);
        }

        return Result.Success();
    }
}
