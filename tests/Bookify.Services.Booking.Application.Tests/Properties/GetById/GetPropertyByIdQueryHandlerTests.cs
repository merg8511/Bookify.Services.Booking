using Bookify.Services.Booking.Application.Common.Pagination;
using Bookify.Services.Booking.Application.Common.Sorting;
using Bookify.Services.Booking.Application.Properties;
using Bookify.Services.Booking.Application.Properties.GetById;
using Bookify.Services.Booking.Application.Properties.GetPaged;
using Bookify.Services.Booking.Application.Properties.ReadModels;
using Bookify.Services.Booking.Domain.Shared;

namespace Bookify.Services.Booking.Application.Tests.Properties.GetById;

public sealed class GetPropertyByIdQueryHandlerTests
{
    [Fact]
    public async Task HandleAsync_WhenPropertyExists_ShouldReturnResponse()
    {
        // ARRANGE
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        Guid propertyId = Guid.NewGuid();

        var expectedResponse = new PropertyDetailsReadModel
        {
            Id = propertyId,
            Name = "Rancho Costa Azul",
            TimeZoneId = "America/El_Salvador",
            CheckInTime = new TimeOnly(15, 0),
            CheckOutTime = new TimeOnly(11, 0),
            IsActive = true
        };

        var propertyReadService = new StubPropertyReadService(expectedResponse);
        var handler = new GetPropertyByIdQueryHandler(propertyReadService);

        var query = new GetPropertyByIdQuery(propertyId);

        // ACT
        Result<PropertyDetailsReadModel> result = await handler.HandleAsync(query, cancellationToken);

        // ASSERT
        Assert.True(result.IsSuccess);

        Assert.Equal(
            expectedResponse,
            result.Value);

        Assert.True(
            propertyReadService.WasCalled);

        Assert.Equal(
            propertyId,
            propertyReadService.RequestedPropertyId);
    }

    [Fact]
    public async Task HandleAsync_WhenPropertyDoesNotExist_ShouldReturnNotFound()
    {
        // ARRANGE
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        Guid propertyId = Guid.NewGuid();

        var propertyReadService = new StubPropertyReadService(response: null);
        var handler = new GetPropertyByIdQueryHandler(propertyReadService);
        var query = new GetPropertyByIdQuery(propertyId);

        // ACT
        Result<PropertyDetailsReadModel> result = await handler.HandleAsync(query, cancellationToken);

        // ASSERT
        Assert.True(result.IsFailure);

        Assert.Equal(
            "Property.NotFound",
            result.Error.Code);

        Assert.Equal(
            ErrorType.NotFound,
            result.Error.Type);

        Assert.True(propertyReadService.WasCalled);
    }

    [Fact]
    public async Task HandleAsync_WithNullQuery_ShouldThrow()
    {
        // ARRANGE
        var handler =
            new GetPropertyByIdQueryHandler(
                new StubPropertyReadService(
                    response: null));

        // ACT
        Task Action()
        {
            return handler.HandleAsync(null!);
        }

        // ASSERT
        await Assert.ThrowsAsync<
            ArgumentNullException>(Action);
    }

    [Fact]
    public async Task HandleAsync_ShouldPropagateCancellationToken()
    {
        // ARRANGE
        var propertyReadService =
            new StubPropertyReadService(
                response: null);

        var handler =
            new GetPropertyByIdQueryHandler(
                propertyReadService);

        var query =
            new GetPropertyByIdQuery(
                Guid.NewGuid());

        using var cancellationTokenSource =
            new CancellationTokenSource();

        await cancellationTokenSource.CancelAsync();

        // ACT
        Task Action()
        {
            return handler.HandleAsync(
                query,
                cancellationTokenSource.Token);
        }

        // ASSERT
        await Assert.ThrowsAsync<
            OperationCanceledException>(Action);
    }

    private sealed class StubPropertyReadService : IPropertyReadService
    {
        private readonly PropertyDetailsReadModel? _response;

        public StubPropertyReadService(PropertyDetailsReadModel? response)
        {
            _response = response;
        }

        public bool WasCalled { get; private set; }
        public Guid? RequestedPropertyId { get; private set; }

        public Task<PropertyDetailsReadModel?> GetByIdAsync(
            Guid propertyId,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            WasCalled = true;
            RequestedPropertyId = propertyId;

            return Task.FromResult(_response);
        }

        public Task<PagedResult<PropertyListItemReadModel>>
            GetPagedAsync(
            int pageNumber,
            int pageSize,
            string? name,
            bool? isActive,
            PropertySortField sortBy,
            SortDirection sortDirection,
            CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }
    }
}
