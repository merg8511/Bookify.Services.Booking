using Bookify.Services.Booking.Application.Abstractions.Messaging;
using Bookify.Services.Booking.Application.Common.Pagination;
using Bookify.Services.Booking.Domain.Shared;

namespace Bookify.Services.Booking.Application.Properties.GetPaged;

public sealed class GetPropertiesQueryValidator
    : IRequestValidator<GetPropertiesQuery>
{
    public Result Validate(GetPropertiesQuery request)
    {
        if (request.PageNumber < 1)
        {
            return Result.Failure(
                PaginationErrors.InvalidPageNumber);
        }

        if (request.PageSize < 1)
        {
            return Result.Failure(
                PaginationErrors.InvalidPageSize);
        }

        if (request.PageSize >
            PaginationDefaults.MaximumPageSize)
        {
            return Result.Failure(
                PaginationErrors.PageSizeExceeded);
        }

        if (!PropertySorting.TryParseSortField(request.SortBy, out _))
        {
            return Result.Failure(GetPropertiesQueryErrors.InvalidSortBy);
        }

        if (!PropertySorting.TryParseSortDirection(
            request.SortDirection, out _))
        {
            return Result.Failure(
                GetPropertiesQueryErrors.InvalidSortDirection);
        }

        return Result.Success();
    }
}
