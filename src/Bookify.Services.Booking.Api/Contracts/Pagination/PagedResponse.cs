namespace Bookify.Services.Booking.Api.Contracts.Pagination;

public sealed record PagedResponse<T>(
    IReadOnlyList<T> Items,
    int PageNumber,
    int PageSize,
    long TotalRecords,
    long TotalPages);
