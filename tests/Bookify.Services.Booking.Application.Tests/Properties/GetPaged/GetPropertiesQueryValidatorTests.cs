namespace Bookify.Services.Booking.Application.Tests.Properties.GetPaged;

using Bookify.Services.Booking.Application.Common.Pagination;
using Bookify.Services.Booking.Application.Properties.GetPaged;
using Xunit;

public sealed class GetPropertiesQueryValidatorTests
{
    private readonly GetPropertiesQueryValidator _validator = new();

    [Fact]
    public void Validate_WithValidPagination_ReturnsSuccess()
    {
        var query = new GetPropertiesQuery(1, 20);

        var result = _validator.Validate(query);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void Validate_WithInvalidPageNumber_ReturnsFailure()
    {
        var query = new GetPropertiesQuery(0, 20);
        var result = _validator.Validate(query);

        Assert.True(result.IsFailure);

        Assert.Equal(
            "Pagination.InvalidPageNumber",
            result.Error.Code);
    }

    [Fact]
    public void Validate_WithInvalidPageSize_ReturnsFailure()
    {
        var query = new GetPropertiesQuery(1, 0);
        var result = _validator.Validate(query);

        Assert.True(result.IsFailure);

        Assert.Equal(
            "Pagination.InvalidPageSize",
            result.Error.Code);
    }

    [Fact]
    public void Validate_WithPageSizeAboveLimit_ReturnsFailure()
    {
        var query = new GetPropertiesQuery(1, PaginationDefaults.MaximumPageSize + 1);
        var result = _validator.Validate(query);

        Assert.True(
            result.IsFailure);

        Assert.Equal(
            "Pagination.PageSizeExceeded",
            result.Error.Code);
    }
}
