
using System.Diagnostics;
using Bookify.Services.Booking.Api.Observability;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.Extensions.Logging;

namespace Bookify.Services.Booking.Integration.Tests.Observability;

public sealed class RequestLoggingMiddlewareTests
{
    [Fact]
    public async Task SuccessfulRequest_ShouldLogStructuredCompletion()
    {
        var logger =
            new CapturingLogger<RequestLoggingMiddleware>();

        RequestDelegate next = context =>
        {
            context.SetEndpoint(
                CreateEndpoint("/api/v1/bookings/{bookingId:guid}"));

            context.Response.StatusCode = 200;

            return Task.CompletedTask;
        };

        var middleware =
            new RequestLoggingMiddleware(next, logger);

        var context = new DefaultHttpContext();

        context.Request.Method = HttpMethods.Get;
        context.Request.Path = "/api/v1/bookings/123";
        context.Request.QueryString =
            new QueryString("?token=secret-value");

        await middleware.InvokeAsync(context);

        CapturedLog entry = Assert.Single(logger.Entries);

        Assert.Equal(LogLevel.Information, entry.Level);
        Assert.Equal(2020, entry.EventId.Id);

        Assert.Equal(
            "HttpRequestCompleted",
            entry.EventId.Name);

        Assert.Equal(
            "GET",
            entry.Properties["HttpMethod"]);

        Assert.Equal(
            "/api/v1/bookings/{bookingId:guid}",
            entry.Properties["RouteTemplate"]);

        Assert.Equal(
            200,
            entry.Properties["StatusCode"]);

        double duration =
            Assert.IsType<double>(
                entry.Properties["DurationMs"]);

        Assert.True(duration >= 0);

        Assert.DoesNotContain(
            "secret-value",
            entry.Message, StringComparison.Ordinal);

        Assert.DoesNotContain(
            "123",
            entry.Properties["RouteTemplate"]?.ToString(),StringComparison.Ordinal);
    }

    [Fact]
    public async Task UnknownRoute_ShouldUseUnmatchedTemplate()
    {
        var logger =
            new CapturingLogger<RequestLoggingMiddleware>();

        RequestDelegate next = context =>
        {
            context.Response.StatusCode = 404;
            return Task.CompletedTask;
        };

        var middleware =
            new RequestLoggingMiddleware(next, logger);

        var context = new DefaultHttpContext();

        context.Request.Method = HttpMethods.Get;
        context.Request.Path = "/private/customer@example.com";

        await middleware.InvokeAsync(context);

        CapturedLog entry = Assert.Single(logger.Entries);

        Assert.Equal(2020, entry.EventId.Id);

        Assert.Equal(
            "unmatched",
            entry.Properties["RouteTemplate"]);

        Assert.DoesNotContain(
            "customer@example.com",
            entry.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ServerErrorResponse_ShouldUseErrorLevel()
    {
        var logger =
            new CapturingLogger<RequestLoggingMiddleware>();

        RequestDelegate next = context =>
        {
            context.Response.StatusCode = 503;
            return Task.CompletedTask;
        };

        var middleware =
            new RequestLoggingMiddleware(next, logger);

        var context = new DefaultHttpContext();
        context.Request.Method = HttpMethods.Post;

        await middleware.InvokeAsync(context);

        CapturedLog entry = Assert.Single(logger.Entries);

        Assert.Equal(LogLevel.Error, entry.Level);
        Assert.Equal(2020, entry.EventId.Id);
        Assert.Equal(503, entry.Properties["StatusCode"]);
    }

    [Fact]
    public async Task UnexpectedException_ShouldLogAndRethrow()
    {
        var logger =
            new CapturingLogger<RequestLoggingMiddleware>();

        var exception =
            new InvalidOperationException(
                "Simulated infrastructure failure");

        RequestDelegate next = _ =>
            Task.FromException(exception);

        var middleware =
            new RequestLoggingMiddleware(next, logger);

        var context = new DefaultHttpContext();
        context.Request.Method = HttpMethods.Post;

        InvalidOperationException thrown =
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => middleware.InvokeAsync(context));

        Assert.Same(exception, thrown);

        CapturedLog entry = Assert.Single(logger.Entries);

        Assert.Equal(LogLevel.Error, entry.Level);
        Assert.Equal(2021, entry.EventId.Id);
        Assert.Same(exception, entry.Exception);

        Assert.Equal(
            500,
            entry.Properties["StatusCode"]);
    }

    [Fact]
    public async Task RequestBodyAndCredentials_ShouldNotAppearInStructuredLog()
    {
        var logger =
            new CapturingLogger<RequestLoggingMiddleware>();

        RequestDelegate next = context =>
        {
            context.Response.StatusCode = 201;
            return Task.CompletedTask;
        };

        var middleware =
            new RequestLoggingMiddleware(next, logger);

        var context = new DefaultHttpContext();

        context.Request.Method = HttpMethods.Post;
        context.Request.Path = "/api/v1/bookings";
        context.Request.Headers.Authorization =
            "Bearer secret-jwt-token";

        context.Request.Headers["Stripe-Signature"] =
            "secret-stripe-signature";

        context.Request.Headers["Booking-Guest-Token"] =
            "secret-guest-token";

        context.Request.Body = new MemoryStream(
            System.Text.Encoding.UTF8.GetBytes(
                """
                {
                  "guestEmail": "private@example.com",
                  "client_secret": "secret-payment-value"
                }
                """));

        await middleware.InvokeAsync(context);

        CapturedLog entry = Assert.Single(logger.Entries);

        string serialized = System.Text.Json.JsonSerializer.Serialize(
            new
            {
                entry.Message,
                entry.Properties
            });

        Assert.DoesNotContain(
            "secret-jwt-token", serialized, StringComparison.Ordinal);

        Assert.DoesNotContain(
            "secret-stripe-signature", serialized, StringComparison.Ordinal);

        Assert.DoesNotContain(
            "secret-guest-token", serialized, StringComparison.Ordinal);

        Assert.DoesNotContain(
            "private@example.com", serialized, StringComparison.Ordinal);

        Assert.DoesNotContain(
            "secret-payment-value", serialized, StringComparison.Ordinal);
    }

    private static Endpoint CreateEndpoint(string template)
    {
        var builder = new RouteEndpointBuilder(
            _ => Task.CompletedTask,
            RoutePatternFactory.Parse(template),
            order: 0);

        return builder.Build();
    }
}
