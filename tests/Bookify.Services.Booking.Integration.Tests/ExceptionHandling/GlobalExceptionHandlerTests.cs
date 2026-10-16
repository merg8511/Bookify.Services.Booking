
using Bookify.Services.Booking.Api.ExceptionHandling;
using Bookify.Services.Booking.Api.Extensions;
using Bookify.Services.Booking.Api.Observability;
using Bookify.Services.Booking.Domain.Shared;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Text.Json;

namespace Bookify.Services.Booking.Integration.Tests.ExceptionHandling;

[Trait("Category", "Integration")]
public sealed class GlobalExceptionHandlerTests
{
    private const string CorrelationHeaderName =
        "X-Correlation-ID";

    private const string SensitiveValue =
        "sk_test_secret_do_not_expose";

    [Fact]
    public async Task UnexpectedException_ShouldReturnSafeProblemDetails()
    {
        // Arrange
        CancellationToken cancellationToken =
            TestContext.Current.CancellationToken;

        const string expectedCorrelationId =
            "booking-exception-20.2";

        await using WebApplication app = CreateTestApp();

        await app.StartAsync(cancellationToken);

        using HttpClient client = app.GetTestClient();

        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            "/unexpected");

        request.Headers.Add(
            CorrelationHeaderName,
            expectedCorrelationId);

        // Act
        using HttpResponseMessage response =
            await client.SendAsync(
                request,
                cancellationToken);

        string body = await response.Content
            .ReadAsStringAsync(cancellationToken);

        using JsonDocument document =
            JsonDocument.Parse(body);

        JsonElement root = document.RootElement;

        // Assert - HTTP
        Assert.Equal(
            HttpStatusCode.InternalServerError,
            response.StatusCode);

        Assert.Equal(
            "application/problem+json",
            response.Content.Headers.ContentType?.MediaType);

        // Assert - Problem Details
        Assert.Equal(
            "urn:bookify:problem-type:server-error",
            root.GetProperty("type").GetString());

        Assert.Equal(
            "Server error",
            root.GetProperty("title").GetString());

        Assert.Equal(
            500,
            root.GetProperty("status").GetInt32());

        Assert.Equal(
            "An unexpected error occurred.",
            root.GetProperty("detail").GetString());

        Assert.Equal(
            "Server.Unexpected",
            root.GetProperty("code").GetString());

        // Assert - Trace ID
        string? traceId = root
            .GetProperty("traceId")
            .GetString();

        Assert.False(string.IsNullOrWhiteSpace(traceId));

        // Assert - Correlation ID
        Assert.Equal(
            expectedCorrelationId,
            root.GetProperty("correlationId").GetString());

        Assert.True(
            response.Headers.TryGetValues(
                CorrelationHeaderName,
                out IEnumerable<string>? headerValues));

        Assert.Equal(
            expectedCorrelationId,
            Assert.Single(headerValues));

        // Assert - Privacy
        Assert.DoesNotContain(
            SensitiveValue,
            body,
            StringComparison.Ordinal);

        Assert.DoesNotContain(
            nameof(InvalidOperationException),
            body,
            StringComparison.Ordinal);

        Assert.DoesNotContain(
            "StackTrace",
            body,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExpectedError_ShouldPreserveConflictResponse()
    {
        // Arrange
        CancellationToken cancellationToken =
            TestContext.Current.CancellationToken;

        await using WebApplication app = CreateTestApp();

        await app.StartAsync(cancellationToken);

        using HttpClient client = app.GetTestClient();

        // Act
        using HttpResponseMessage response =
            await client.GetAsync(
                "/expected",
                cancellationToken);

        string body = await response.Content
            .ReadAsStringAsync(cancellationToken);

        using JsonDocument document =
            JsonDocument.Parse(body);

        JsonElement root = document.RootElement;

        // Assert
        Assert.Equal(
            HttpStatusCode.Conflict,
            response.StatusCode);

        Assert.Equal(
            "application/problem+json",
            response.Content.Headers.ContentType?.MediaType);

        Assert.Equal(
            409,
            root.GetProperty("status").GetInt32());

        Assert.Equal(
            "Bookings.TestConflict",
            root.GetProperty("code").GetString());

        Assert.Equal(
            "The booking is in conflict.",
            root.GetProperty("detail").GetString());
    }

    private static WebApplication CreateTestApp()
    {
        WebApplicationBuilder builder =
            WebApplication.CreateBuilder(
                new WebApplicationOptions
                {
                    EnvironmentName = "Testing"
                });

        builder.WebHost.UseTestServer();

        builder.Services.AddProblemDetails();

        builder.Services
            .AddExceptionHandler<GlobalExceptionHandler>();

        WebApplication app = builder.Build();

        app.UseMiddleware<CorrelationIdMiddleware>();

        app.UseExceptionHandler();

        app.UseMiddleware<RequestLoggingMiddleware>();

        app.UseRouting();

        app.MapGet(
            "/unexpected",
            () => Task.FromException(
                new InvalidOperationException(
                    $"Provider failed. Secret={SensitiveValue}")));

        app.MapGet(
            "/expected",
            (HttpContext httpContext) =>
                Error.Conflict(
                    "Bookings.TestConflict",
                    "The booking is in conflict.")
                .ToProblem(httpContext));

        return app;
    }
}
