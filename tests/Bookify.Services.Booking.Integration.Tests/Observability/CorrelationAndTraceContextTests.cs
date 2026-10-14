
using System.Net;
using System.Net.Http;
using Bookify.Services.Booking.Integration.Tests.Infrastructure;

namespace Bookify.Services.Booking.Integration.Tests.Observability;

[Collection(BookingApiTestFixture.Name)]
[Trait("Category", "Integration")]
public sealed class CorrelationAndTraceContextTests
{
    private const string CorrelationHeader =
        "X-Correlation-ID";

    private readonly BookingApiFactory _factory;

    public CorrelationAndTraceContextTests(
        BookingApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task SeparateRequests_ShouldReceiveDifferentGeneratedCorrelationIds()
    {
        CancellationToken cancellationToken =
            TestContext.Current.CancellationToken;

        using HttpResponseMessage first =
            await _factory.Client.GetAsync(
                "/health",
                cancellationToken);

        using HttpResponseMessage second =
            await _factory.Client.GetAsync(
                "/health",
                cancellationToken);

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);

        string firstId = GetCorrelationId(first);
        string secondId = GetCorrelationId(second);

        Assert.Matches("^[a-f0-9]{32}$", firstId);
        Assert.Matches("^[a-f0-9]{32}$", secondId);
        Assert.NotEqual(firstId, secondId);
    }

    [Fact]
    public async Task ValidCorrelationId_ShouldRemainIndependentFromTraceparent()
    {
        CancellationToken cancellationToken =
            TestContext.Current.CancellationToken;

        const string correlationId = "bookify-correlation-001";

        const string traceparent =
            "00-3c941fa8ea7b4c91a719d868f89e628d-" +
            "61b79d4d289e7c30-01";

        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            "/health");

        request.Headers.TryAddWithoutValidation(
            CorrelationHeader,
            correlationId);

        request.Headers.TryAddWithoutValidation(
            "traceparent",
            traceparent);

        using HttpResponseMessage response =
            await _factory.Client.SendAsync(
                request,
                cancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        Assert.Equal(
            correlationId,
            GetCorrelationId(response));

        Assert.NotEqual(
            "3c941fa8ea7b4c91a719d868f89e628d",
            GetCorrelationId(response));
    }

    [Fact]
    public async Task UnknownRoute_ShouldStillReturnCorrelationId()
    {
        CancellationToken cancellationToken =
            TestContext.Current.CancellationToken;

        const string expectedCorrelationId =
            "observability-not-found-001";

        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            "/api/v1/unmatched-observability-test");

        request.Headers.TryAddWithoutValidation(
            CorrelationHeader,
            expectedCorrelationId);

        using HttpResponseMessage response =
            await _factory.Client.SendAsync(
                request,
                cancellationToken);

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
                CorrelationHeader,
                out IEnumerable<string>? values));

        return Assert.Single(values!);
    }
}
