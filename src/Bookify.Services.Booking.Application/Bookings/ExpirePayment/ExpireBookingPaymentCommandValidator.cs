using Bookify.Services.Booking.Application.Abstractions.Messaging;
using Bookify.Services.Booking.Domain.Shared;

namespace Bookify.Services.Booking.Application.Bookings.ExpirePayment;

public sealed class ExpireBookingPaymentCommandValidator
    : IRequestValidator<ExpireBookingPaymentCommand>
{
    public Result Validate(ExpireBookingPaymentCommand request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.BookingId == Guid.Empty)
        {
            return Result.Failure(
                ExpireBookingPaymentErrors.InvalidBookingId);
        }

        return Result.Success();
    }
}
