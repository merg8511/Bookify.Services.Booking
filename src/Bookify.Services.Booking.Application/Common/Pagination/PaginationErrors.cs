using Bookify.Services.Booking.Domain.Shared;

namespace Bookify.Services.Booking.Application.Common.Pagination;

internal static class PaginationErrors
{
    internal static readonly Error InvalidPageNumber =
        new(
            "Pagination.InvalidPageNumber",
            "Page number must be grater than zero",
            ErrorType.Validation);

    internal static readonly Error InvalidPageSize =
        new(
            "Pagination.InvalidPageSize",
            "Page size must be grater than zero",
            ErrorType.Validation);

    internal static readonly Error PageSizeExceeded =
        new(
            "Pagination.PageSizeExceeded",
            $"Page size cannot exceed " +
            $"{PaginationDefaults.MaximumPageSize}.",
            ErrorType.Validation);
}
