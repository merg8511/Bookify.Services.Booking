using Bookify.Services.Booking.Application.Common.Pagination;

namespace Bookify.Services.Booking.Application.Tests.Common.Pagination;

public sealed class PagedResultTests
{
    [Theory]
    [InlineData(0, 10, 0)]
    [InlineData(1, 10, 1)]
    [InlineData(10, 10, 1)]
    [InlineData(11, 10, 2)]
    [InlineData(20, 10, 2)]
    [InlineData(21, 10, 3)]
    public void TotalPages_ReturnsExpectedValue(
        long totalRecords,
        int pageSize,
        long expectedTotalPages)
    {
        var result =
            new PagedResult<int>(
                [],
                1,
                pageSize,
                totalRecords);

        Assert.Equal(
            expectedTotalPages,
            result.TotalPages);
    }
}
