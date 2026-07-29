using Bookify.Services.Booking.Application.Common.Pagination;
using Bookify.Services.Booking.Application.Common.Sorting;
using Bookify.Services.Booking.Application.Properties;
using Bookify.Services.Booking.Application.Properties.GetPaged;
using Bookify.Services.Booking.Application.Properties.ReadModels;

namespace Bookify.Services.Booking.Application.Tests.Properties.GetPaged;

public sealed class GetPropertiesQueryHandlerTests
{
    [Fact]
    public async Task HandleAsync_NormalizesNameFilter()
    {
        var readService = new PropertyReadServiceSpy();

        var handler = new GetPropertiesQueryHandler(readService);

        var query =
            new GetPropertiesQuery(
                1,
                20,
                " rancho ",
                true,
                null,
                null);

        var result = await handler.HandleAsync(query);

        Assert.True(result.IsSuccess);

        Assert.Equal(
            "rancho",
            readService.ReceivedName);

        Assert.True(readService.ReceivedIsActive);
    }

    [Fact]
    public async Task HandleAsync_WithWhitespaceName_RemovesNameFilter()
    {
        var readService = new PropertyReadServiceSpy();
        var handler = new GetPropertiesQueryHandler(readService);

        var query = new GetPropertiesQuery(
            1,
            20,
            "  ",
            null,
            null,
            null);

        var result = await handler.HandleAsync(query);

        Assert.True(result.IsSuccess);
        Assert.Null(readService.ReceivedName);
        Assert.Null(readService.ReceivedIsActive);
    }

    [Fact]
    public async Task HandleAsync_WithMissingSorting_UsesDefaults()
    {
        var readService =
            new PropertyReadServiceSpy();

        var handler =
            new GetPropertiesQueryHandler(
                readService);

        var query =
            new GetPropertiesQuery(
                1,
                20,
                null,
                null,
                null,
                null);

        var result =
            await handler.HandleAsync(
                query);

        Assert.True(
            result.IsSuccess);

        Assert.Equal(
            PropertySortField.Name,
            readService.ReceivedSortField);

        Assert.Equal(
            SortDirection.Ascending,
            readService.ReceivedSortDirection);
    }

    [Fact]
    public async Task HandleAsync_NormalizesSortingValues()
    {
        var readService =
            new PropertyReadServiceSpy();

        var handler =
            new GetPropertiesQueryHandler(
                readService);

        var query =
            new GetPropertiesQuery(
                1,
                20,
                null,
                null,
                "  ISACTIVE  ",
                "  DESC  ");

        var result =
            await handler.HandleAsync(
                query);

        Assert.True(
            result.IsSuccess);

        Assert.Equal(
            PropertySortField.IsActive,
            readService.ReceivedSortField);

        Assert.Equal(
            SortDirection.Descending,
            readService.ReceivedSortDirection);
    }

    private sealed class PropertyReadServiceSpy : IPropertyReadService
    {
        public string? ReceivedName { get; private set; }
        public bool? ReceivedIsActive { get; private set; }
        public PropertySortField ReceivedSortField { get; private set; }

        public SortDirection ReceivedSortDirection { get; private set; }

        public Task<PropertyDetailsReadModel?> GetByIdAsync(
            Guid propertyId,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<
            PagedResult<
                PropertyListItemReadModel>>
            GetPagedAsync(
            int pageNumber,
            int pageSize,
            string? name,
            bool? isActive,
            PropertySortField sortField,
            SortDirection sortDirection,
            CancellationToken cancellationToken = default)
        {
            ReceivedName = name;
            ReceivedIsActive = isActive;
            ReceivedSortField = sortField;
            ReceivedSortDirection = sortDirection;

            var result =
                new PagedResult<
                    PropertyListItemReadModel>(
                    [],
                    pageNumber,
                    pageSize,
                    0);

            return Task.FromResult(result);
        }
    }
}
