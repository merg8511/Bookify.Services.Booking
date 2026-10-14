
using System.Net;
using Bookify.Services.Booking.Integration.Tests.Infrastructure;

namespace Bookify.Services.Booking.Integration.Tests.Observability;

[Collection(BookingApiTestFixture.Name)]
[Trait("Category", "Integration")]
public sealed class CorrelationIdEndpointTests
{
    private const string HeaderName = "X-Correlation-ID";

    private readonly BookingApiFactory _factory;
    private static readonly string[] values = new[]
            {
                "first-correlation",
                "second-correlation"
            };

    public CorrelationIdEndpointTests(
        BookingApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Request_WhenCorrelationIdIsMissing_ShouldGenerateAndReturnOne()
    {
        // Arrange
        CancellationToken cancellationToken =
            TestContext.Current.CancellationToken;

        // Act
        using HttpResponseMessage response =
            await _factory.Client.GetAsync(
                "/health",
                cancellationToken);

        // Assert
        Assert.Equal(
            HttpStatusCode.OK,
            response.StatusCode);

        string correlationId =
            GetCorrelationId(response);

        Assert.Matches(
            "^[a-f0-9]{32}$",
            correlationId);
    }

    [Fact]
    public async Task Request_WhenCorrelationIdIsValid_ShouldPreserveIt()
    {
        // Arrange
        CancellationToken cancellationToken =
            TestContext.Current.CancellationToken;

        const string expectedCorrelationId =
            "booking-flow-19.2";

        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            "/health");

        request.Headers.Add(
            HeaderName,
            expectedCorrelationId);

        // Act
        using HttpResponseMessage response =
            await _factory.Client.SendAsync(
                request,
                cancellationToken);

        // Assert
        Assert.Equal(
            HttpStatusCode.OK,
            response.StatusCode);

        Assert.Equal(
            expectedCorrelationId,
            GetCorrelationId(response));
    }

    [Theory]
    [InlineData("")]
    [InlineData("contains spaces")]
    [InlineData("abc/123")]
    [InlineData("invalid@correlation")]
    [InlineData("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
    public async Task Request_WhenCorrelationIdIsInvalid_ShouldReplaceIt(
        string invalidCorrelationId)
    {
        // Arrange
        CancellationToken cancellationToken =
            TestContext.Current.CancellationToken;

        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            "/health");

        request.Headers.TryAddWithoutValidation(
            HeaderName,
            invalidCorrelationId);

        // Act
        using HttpResponseMessage response =
            await _factory.Client.SendAsync(
                request,
                cancellationToken);

        // Assert
        Assert.Equal(
            HttpStatusCode.OK,
            response.StatusCode);

        string returnedCorrelationId =
            GetCorrelationId(response);

        Assert.NotEqual(
            invalidCorrelationId,
            returnedCorrelationId);

        Assert.Matches(
            "^[a-f0-9]{32}$",
            returnedCorrelationId);
    }

    [Fact]
    public async Task Request_WhenCorrelationIdHasMultipleValues_ShouldGenerateNewOne()
    {
        // Arrange
        CancellationToken cancellationToken =
            TestContext.Current.CancellationToken;

        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            "/health");

        request.Headers.TryAddWithoutValidation(
            HeaderName,
            values);

        // Act
        using HttpResponseMessage response =
            await _factory.Client.SendAsync(
                request,
                cancellationToken);

        // Assert
        Assert.Equal(
            HttpStatusCode.OK,
            response.StatusCode);

        string returnedCorrelationId =
            GetCorrelationId(response);

        Assert.Matches(
            "^[a-f0-9]{32}$",
            returnedCorrelationId);
    }

    [Fact]
    public async Task Request_WhenEndpointDoesNotExist_ShouldStillReturnCorrelationId()
    {
        // Arrange
        CancellationToken cancellationToken =
            TestContext.Current.CancellationToken;

        const string expectedCorrelationId =
            "not-found-request-001";

        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            "/api/v1/this-route-does-not-exist");

        request.Headers.Add(
            HeaderName,
            expectedCorrelationId);

        // Act
        using HttpResponseMessage response =
            await _factory.Client.SendAsync(
                request,
                cancellationToken);

        // Assert
        Assert.Equal(
            HttpStatusCode.NotFound,
            response.StatusCode);

        Assert.Equal(
            expectedCorrelationId,
            GetCorrelationId(response));
    }

    private static string GetCorrelationId(
        HttpResponseMessage response)
    {
        Assert.True(
            response.Headers.TryGetValues(
                HeaderName,
                out IEnumerable<string>? values));

        return Assert.Single(values);
    }
}
