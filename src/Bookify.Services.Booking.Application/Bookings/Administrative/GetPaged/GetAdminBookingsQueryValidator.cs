using Bookify.Services.Booking.Application.Abstractions.Messaging;
using Bookify.Services.Booking.Application.Common.Pagination;
using Bookify.Services.Booking.Domain.Bookings;
using Bookify.Services.Booking.Domain.Shared;

namespace Bookify.Services.Booking.Application.Bookings.Administrative.GetPaged;

public sealed class GetAdminBookingsQueryValidator : IRequestValidator<GetAdminBookingsQuery>
{
    public Result Validate(GetAdminBookingsQuery request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.PropertyId == Guid.Empty)
        {
            return Result.Failure(AdminBookingErrors.InvalidPropertyId);
        }

        if (request.PageNumber < 1)
        {
            return Result.Failure(PaginationErrors.InvalidPageSize);
        }

        if (request.PageSize > PaginationDefaults.MaximumPageSize)
        {
            return Result.Failure(PaginationErrors.PageSizeExceeded);
        }

        if (request.UnitId == Guid.Empty)
        {
            return Result.Failure(AdminBookingErrors.InvalidUnitId);
        }

        if (request.Status is not null)
        {
            bool validStatus = Enum.TryParse<BookingStatus>(request.Status, ignoreCase: false, out BookingStatus status) &&
                               Enum.IsDefined(status) &&
                               string.Equals(request.Status, status.ToString(), StringComparison.Ordinal);

            if (!validStatus)
            {
                return Result.Failure(AdminBookingErrors.InvalidStatus);
            }
        }

        bool hasRangeStart = request.RangeStart.HasValue;
        bool hasRangeEnd = request.RangeEnd.HasValue;

        if (hasRangeStart != hasRangeEnd ||
          (hasRangeStart && request.RangeEnd!.Value <= request.RangeStart!.Value))
        {
            return Result.Failure(AdminBookingErrors.InvalidDateRange);
        }

        if (request.CustomerSubjectId is not null)
        {
            string customerSubjectId = request.CustomerSubjectId;

            if (string.IsNullOrWhiteSpace(customerSubjectId) ||
               customerSubjectId.Length > 255 ||
               !string.Equals(customerSubjectId, customerSubjectId.Trim(), StringComparison.Ordinal))
            {
                return Result.Failure(AdminBookingErrors.InvalidCustomerSubjectId);
            }
        }

        return Result.Success();
    }
}
