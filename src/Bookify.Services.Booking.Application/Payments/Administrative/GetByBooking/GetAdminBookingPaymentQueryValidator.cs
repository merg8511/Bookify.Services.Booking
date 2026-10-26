using Bookify.Services.Booking.Application.Abstractions.Messaging;
using Bookify.Services.Booking.Application.Bookings.Administrative;
using Bookify.Services.Booking.Domain.Shared;

namespace Bookify.Services.Booking.Application.Payments.Administrative.GetByBooking;

public sealed class GetAdminBookingPaymentQueryValidator :
    IRequestValidator<GetAdminBookingPaymentQuery>
{
    public Result Validate(GetAdminBookingPaymentQuery request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.PropertyId == Guid.Empty)
        {
            return Result.Failure(AdminBookingErrors.InvalidPropertyId);
        }

        if (request.BookingId == Guid.Empty)
        {
            return Result.Failure(AdminBookingErrors.InvalidBookingId);
        }

        return Result.Success();
    }
}
