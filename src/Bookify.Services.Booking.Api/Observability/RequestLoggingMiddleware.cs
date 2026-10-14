using System.Diagnostics;

namespace Bookify.Services.Booking.Api.Observability;

internal sealed class RequestLoggingMiddleware
{
    private const string UnmatchedRoute = "unmatched";
    private const string UnnamedEndpoint = "unnamed-endpoint";

    private readonly RequestDelegate _next;
    private readonly ILogger<RequestLoggingMiddleware> _logger;

    public RequestLoggingMiddleware(
        RequestDelegate next,
        ILogger<RequestLoggingMiddleware> logger)
    {
        _next = next ??
            throw new ArgumentNullException(nameof(next));

        _logger = logger ??
            throw new ArgumentNullException(nameof(logger));
    }

    public async Task InvokeAsync(HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        long startedAt = Stopwatch.GetTimestamp();

        try
        {
            await _next(httpContext);
        }
        catch (Exception exception)
        {
            double durationMs = Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds;

            _logger.LogError(
                ApiLogEvents.HttpRequestFailed,
                "HTTP request failed. " +
                "HttpMethod: {HttpMethod}. " +
                "RouteTemplate: {RouteTemplate}. " +
                "StatusCode: {StatusCode}. " +
                "DurationMs: {DurationMs}. " +
                "ExceptionType: {ExceptionType}",
                httpContext.Request.Method,
                GetRouteTemplate(httpContext),
                StatusCodes.Status500InternalServerError,
                durationMs,
                exception.GetType().Name);

            throw;
        }

        double elapsedMs = Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds;
        int statusCode = httpContext.Response.StatusCode;
        string routeTemplate = GetRouteTemplate(httpContext);

        if (statusCode >= StatusCodes.Status500InternalServerError)
        {
            _logger.LogError(
                ApiLogEvents.HttpRequestCompleted,
                "HTTP request completed with server error. " +
                "HttpMethod: {HttpMethod}. " +
                "RouteTemplate: {RouteTemplate}. " +
                "StatusCode: {StatusCode}. " +
                "DurationMs: {DurationMs}",
                httpContext.Request.Method,
                routeTemplate,
                statusCode,
                elapsedMs);

            return;
        }

        _logger.LogInformation(
            ApiLogEvents.HttpRequestCompleted,
            "HTTP request completed. " +
            "HttpMethod: {HttpMethod}. " +
            "RouteTemplate: {RouteTemplate}. " +
            "StatusCode: {StatusCode}. " +
            "DurationMs: {DurationMs}",
            httpContext.Request.Method,
            routeTemplate,
            statusCode,
            elapsedMs);
    }

    private static string GetRouteTemplate(HttpContext httpContext)
    {
        Endpoint? endpoint = httpContext.GetEndpoint();

        if (endpoint is RouteEndpoint routeEndpoint)
        {
            string? routePattern = routeEndpoint.RoutePattern.RawText;

            if (!string.IsNullOrWhiteSpace(routePattern))
            {
                return routePattern.StartsWith('/') ? routePattern : $"/{routePattern}";
            }

            return UnnamedEndpoint;
        }

        return UnmatchedRoute;
    }
}
