using Bookify.Services.Booking.Api.Extensions;
using Bookify.Services.Booking.Api.Observability;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using System.Text.Json;

namespace Bookify.Services.Booking.Api.Timeouts;

internal static class RequestTimeoutProblemWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static Task WriteAsync(HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        var problem = new ProblemDetails
        {
            Type = ProblemTypeUris.RequestTimeout,
            Title = "Request timeout",
            Status = StatusCodes.Status504GatewayTimeout,
            Detail = "The request exceeded the allowed processing time."
        };

        problem.Extensions["code"] = "Http.RequestTimeout";
        problem.Extensions["traceId"] = Activity.Current?.Id ?? httpContext.TraceIdentifier;

        if (httpContext.Items.TryGetValue(CorrelationIdMiddleware.HttpContextItemKey, out object? correlationValue) &&
            correlationValue is string correlationId)
        {
            problem.Extensions["correlationId"] = correlationId;
        }

        httpContext.Response.StatusCode = StatusCodes.Status504GatewayTimeout;
        httpContext.Response.ContentType = "application/problem+json";
        httpContext.Response.Headers.CacheControl = "no-store";

        string json = JsonSerializer.Serialize(problem, JsonOptions);

        return httpContext.Response.WriteAsync(json, CancellationToken.None);
    }
}
