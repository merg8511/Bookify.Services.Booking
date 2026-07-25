using Bookify.Services.Booking.Application.Properties.GetById;
using Bookify.Services.Booking.Domain.Shared;

namespace Bookify.Services.Booking.Application.Tests.Properties.GetById;

public sealed class GetPropertyByIdQueryHandlerTests
{
    [Fact]
    public async Task HandleAsync_WhenPropertyExists_ShouldReturnResponse()
    {
        // ARRANGE
        Guid propertyId = Guid.NewGuid();

        var expectedResponse = new PropertyResponse(
            propertyId,
            "Rancho Costa Azul",
            "America/El_Salvador",
            new TimeOnly(15, 0),
            new TimeOnly(11, 0),
            IsActive: true);

        var propertyReadService = new StubPropertyReadService(expectedResponse);
        var handler = new GetPropertyByIdQueryHandler(propertyReadService);

        var query = new GetPropertyByIdQuery(propertyId);

        // ACT
        Result<PropertyResponse> result = await handler.HandleAsync(query);

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
        Guid propertyId = Guid.NewGuid();

        var propertyReadService = new StubPropertyReadService(response: null);
        var handler = new GetPropertyByIdQueryHandler(propertyReadService);
        var query = new GetPropertyByIdQuery(propertyId);

        // ACT
        Result<PropertyResponse> result = await handler.HandleAsync(query);

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
        private readonly PropertyResponse? _response;

        public StubPropertyReadService(PropertyResponse? response)
        {
            _response = response;
        }

        public bool WasCalled { get; private set; }
        public Guid? RequestedPropertyId { get; private set; }

        public Task<PropertyResponse?> GetByIdAsync(
            Guid propertyId,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            WasCalled = true;
            RequestedPropertyId = propertyId;

            return Task.FromResult(_response);
        }
    }
}
