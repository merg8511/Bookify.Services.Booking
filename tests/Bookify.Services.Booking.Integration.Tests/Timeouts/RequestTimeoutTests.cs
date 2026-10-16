
using Bookify.Services.Booking.Api.ExceptionHandling;
using Bookify.Services.Booking.Api.Observability;
using Bookify.Services.Booking.Api.Timeouts;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Text.Json;

namespace Bookify.Services.Booking.Integration.Tests.Timeouts;

[Trait("Category", "Integration")]
public sealed class RequestTimeoutTests
{
    private const string CorrelationHeaderName =
        "X-Correlation-ID";

    [Fact]
    public async Task SlowRequest_ShouldReturn504ProblemDetails()
    {
        CancellationToken cancellationToken =
            TestContext.Current.CancellationToken;

        await using WebApplication app = CreateTestApp();

        await app.StartAsync(cancellationToken);

        using HttpClient client = app.GetTestClient();

        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            "/slow");

        request.Headers.Add(
            CorrelationHeaderName,
            "booking-timeout-20.3");

        using HttpResponseMessage response =
            await client.SendAsync(
                request,
                cancellationToken);

        string body = await response.Content
            .ReadAsStringAsync(cancellationToken);

        using JsonDocument document =
            JsonDocument.Parse(body);

        JsonElement root = document.RootElement;

        Assert.Equal(
            HttpStatusCode.GatewayTimeout,
            response.StatusCode);

        Assert.Equal(
            "application/problem+json",
            response.Content.Headers.ContentType?.MediaType);

        Assert.Equal(
            "urn:bookify:problem-type:request-timeout",
            root.GetProperty("type").GetString());

        Assert.Equal(
            "Request timeout",
            root.GetProperty("title").GetString());

        Assert.Equal(
            504,
            root.GetProperty("status").GetInt32());

        Assert.Equal(
            "Http.RequestTimeout",
            root.GetProperty("code").GetString());

        Assert.Equal(
            "The request exceeded the allowed processing time.",
            root.GetProperty("detail").GetString());

        Assert.False(
            string.IsNullOrWhiteSpace(
                root.GetProperty("traceId").GetString()));

        Assert.Equal(
            "booking-timeout-20.3",
            root.GetProperty("correlationId").GetString());

        Assert.True(
            response.Headers.TryGetValues(
                CorrelationHeaderName,
                out IEnumerable<string>? values));

        Assert.Equal(
            "booking-timeout-20.3",
            Assert.Single(values));

        Assert.DoesNotContain(
            "OperationCanceledException",
            body,
            StringComparison.Ordinal);

        Assert.DoesNotContain(
            "StackTrace",
            body,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task FastRequest_ShouldCompleteNormally()
    {
        CancellationToken cancellationToken =
            TestContext.Current.CancellationToken;

        await using WebApplication app = CreateTestApp();

        await app.StartAsync(cancellationToken);

        using HttpClient client = app.GetTestClient();

        using HttpResponseMessage response =
            await client.GetAsync(
                "/fast",
                cancellationToken);

        Assert.Equal(
            HttpStatusCode.OK,
            response.StatusCode);

        Assert.True(
            response.Headers.Contains(
                CorrelationHeaderName));
    }

    [Fact]
    public async Task NamedPolicy_ShouldOverrideDefaultTimeout()
    {
        CancellationToken cancellationToken =
            TestContext.Current.CancellationToken;

        await using WebApplication app = CreateTestApp();

        await app.StartAsync(cancellationToken);

        using HttpClient client = app.GetTestClient();

        using HttpResponseMessage response =
            await client.GetAsync(
                "/payment-policy",
                cancellationToken);

        // The endpoint exceeds the 200 ms default timeout
        // but remains below the 3-second payment policy.
        Assert.Equal(
            HttpStatusCode.OK,
            response.StatusCode);
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

        builder.Configuration.AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["RequestTimeouts:Default"] =
                    "00:00:00.200",

                ["RequestTimeouts:PaymentInitiation"] =
                    "00:00:03",

                ["RequestTimeouts:StripeWebhook"] =
                    "00:00:01"
            });

        builder.Services.AddProblemDetails();

        builder.Services
            .AddExceptionHandler<GlobalExceptionHandler>();

        builder.Services.AddBookifyRequestTimeouts(
            builder.Configuration);

        WebApplication app = builder.Build();

        app.UseMiddleware<CorrelationIdMiddleware>();

        app.UseExceptionHandler();

        app.UseMiddleware<RequestLoggingMiddleware>();

        app.UseRouting();

        app.UseRequestTimeouts();

        app.MapGet(
            "/slow",
            async (CancellationToken cancellationToken) =>
            {
                await Task.Delay(
                    TimeSpan.FromSeconds(10),
                    cancellationToken);

                return Results.Ok();
            });

        app.MapGet(
            "/fast",
            () => Results.Ok());

        app.MapGet(
            "/payment-policy",
            async (CancellationToken cancellationToken) =>
            {
                await Task.Delay(
                    TimeSpan.FromMilliseconds(350),
                    cancellationToken);

                return Results.Ok();
            })
            .WithRequestTimeout(
                BookifyRequestTimeoutPolicies.PaymentInitiation);

        return app;
    }
}
