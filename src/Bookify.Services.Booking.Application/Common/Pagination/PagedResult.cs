namespace Bookify.Services.Booking.Application.Common.Pagination;

public sealed class PagedResult<T>
{
    public PagedResult(
        IReadOnlyList<T> items,
        int pageNumber,
        int pageSize,
        long totalRecords)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentOutOfRangeException.ThrowIfLessThan(pageNumber, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(pageSize, 1);
        ArgumentOutOfRangeException.ThrowIfNegative(totalRecords);

        Items = items;
        PageNumber = pageNumber;
        PageSize = pageSize;
        TotalRecords = totalRecords;
    }

    public IReadOnlyList<T> Items { get; }
    public int PageNumber { get; }
    public int PageSize { get; }
    public long TotalRecords { get; }
    public long TotalPages
    {
        get
        {
            if (TotalRecords == 0)
            {
                return 0;
            }

            long completePages = TotalRecords / PageSize;
            bool hasPartialPage = TotalRecords % PageSize != 0;

            return hasPartialPage
                ? completePages + 1
                : completePages;
        }
    }

}
