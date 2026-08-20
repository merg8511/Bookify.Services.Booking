using Bookify.Services.Booking.Application.Abstractions.Messaging;
using Bookify.Services.Booking.Domain.Shared;

namespace Bookify.Services.Booking.Application.Bookings.MarkAsPaid;

public sealed class MarkBookingAsPaidCommandValidator
    : IRequestValidator<MarkBookingAsPaidCommand>
{
    public Result Validate(MarkBookingAsPaidCommand request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.BookingId == Guid.Empty)
        {
            return Result.Failure(
                MarkBookingAsPaidErrors.InvalidBookingId);
        }

        return Result.Success();
    }
}
