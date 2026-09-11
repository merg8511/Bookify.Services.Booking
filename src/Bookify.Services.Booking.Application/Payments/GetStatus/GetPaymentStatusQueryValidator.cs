using Bookify.Services.Booking.Application.Abstractions.Messaging;
using Bookify.Services.Booking.Domain.Shared;

namespace Bookify.Services.Booking.Application.Payments.GetStatus;

public sealed class GetPaymentStatusQueryValidator : IRequestValidator<GetPaymentStatusQuery>
{
    public Result Validate(GetPaymentStatusQuery request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.BookingId == Guid.Empty)
        {
            return Result.Failure(GetPaymentStatusErrors.InvalidBookingId);
        }

        return Result.Success();
    }
}
