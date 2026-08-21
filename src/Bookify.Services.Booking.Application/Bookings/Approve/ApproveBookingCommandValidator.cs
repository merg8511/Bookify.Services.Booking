using Bookify.Services.Booking.Application.Abstractions.Messaging;
using Bookify.Services.Booking.Domain.Shared;

namespace Bookify.Services.Booking.Application.Bookings.Approve;

public sealed class ApproveBookingCommandValidator
    : IRequestValidator<ApproveBookingCommand>
{
    public Result Validate(ApproveBookingCommand request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.BookingId == Guid.Empty)
        {
            return Result.Failure(
                ApproveBookingErrors.InvalidBookingId);
        }

        return Result.Success();
    }
}
