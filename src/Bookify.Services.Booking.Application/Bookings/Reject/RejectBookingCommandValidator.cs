using Bookify.Services.Booking.Application.Abstractions.Messaging;
using Bookify.Services.Booking.Domain.Shared;

namespace Bookify.Services.Booking.Application.Bookings.Reject;

public sealed class RejectBookingCommandValidator : IRequestValidator<RejectBookingCommand>
{
    public Result Validate(RejectBookingCommand request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.BookingId == Guid.Empty)
        {
            return Result.Failure(
                RejectBookingErrors.InvalidBookingId);
        }

        return Result.Success();
    }
}
