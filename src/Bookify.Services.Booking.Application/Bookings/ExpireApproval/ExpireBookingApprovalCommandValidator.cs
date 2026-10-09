using Bookify.Services.Booking.Application.Abstractions.Messaging;
using Bookify.Services.Booking.Domain.Shared;

namespace Bookify.Services.Booking.Application.Bookings.ExpireApproval;

internal sealed class ExpireBookingApprovalCommandValidator : IRequestValidator<ExpireBookingApprovalCommand>
{
    public Result Validate(ExpireBookingApprovalCommand request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.BookingId == Guid.Empty)
        {
            return Result.Failure(ExpireBookingApprovalErrors.InvalidBookingId);
        }

        return Result.Success();
    }
}
