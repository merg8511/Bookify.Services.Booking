using Bookify.Services.Booking.Application.Abstractions.Messaging;
using Bookify.Services.Booking.Domain.Shared;

namespace Bookify.Services.Booking.Application.Bookings.Administrative.GetById;

public sealed class GetAdminBookingByIdQueryValidator : IRequestValidator<GetAdminBookingByIdQuery>
{
    public Result Validate(GetAdminBookingByIdQuery request)
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
