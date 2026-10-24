using Bookify.Services.Booking.Application.Abstractions.Messaging;
using Bookify.Services.Booking.Domain.Shared;

namespace Bookify.Services.Booking.Application.Bookings.Administrative.Calendar;

public sealed class GetAdminBookingCalendarQueryValidator :
    IRequestValidator<GetAdminBookingCalendarQuery>
{
    private const int MaximumRangeDays = 366;

    public Result Validate(GetAdminBookingCalendarQuery request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.PropertyId == Guid.Empty)
        {
            return Result.Failure(AdminBookingErrors.InvalidPropertyId);
        }

        if (!request.RangeStart.HasValue ||
           !request.RangeEnd.HasValue)
        {
            return Result.Failure(AdminBookingCalendarErrors.InvalidDateRange);
        }

        DateOnly rangeStart = request.RangeStart.Value;
        DateOnly rangeEnd = request.RangeEnd.Value;

        if (rangeEnd <= rangeStart)
        {
            return Result.Failure(AdminBookingCalendarErrors.InvalidDateRange);
        }

        int rangeDays = rangeEnd.DayNumber - rangeStart.DayNumber;

        if (rangeDays > MaximumRangeDays)
        {
            return Result.Failure(AdminBookingCalendarErrors.RangeTooLarge);
        }

        if (request.UnitId.HasValue &&
           request.UnitId.Value == Guid.Empty)
        {
            return Result.Failure(AdminBookingErrors.InvalidUnitId);
        }

        return Result.Success();
    }
}
