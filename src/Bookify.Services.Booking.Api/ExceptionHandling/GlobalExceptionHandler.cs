using Bookify.Services.Booking.Api.Extensions;
using Bookify.Services.Booking.Api.Observability;
using Microsoft.AspNetCore.Diagnostics;
using System.Diagnostics;

namespace Bookify.Services.Booking.Api.ExceptionHandling;

internal sealed class GlobalExceptionHandler : IExceptionHandler
{
    private const string UnexpectedErrorCode = "Server.Unexpected";
    private const string CodeExtensionName = "code";
    private const string TraceIdExtensionName = "traceId";
    private const string CorrelationIdExtensionName = "correlationId";


    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        ArgumentNullException.ThrowIfNull(exception);

        if (exception is OperationCanceledException &&
            httpContext.RequestAborted.IsCancellationRequested)
        {
            return false;
        }

        if (httpContext.Response.HasStarted)
        {
            return false;
        }

        var extensions = new Dictionary<string, object?>
        {
            [CodeExtensionName] = UnexpectedErrorCode,
            [TraceIdExtensionName] = Activity.Current?.Id ?? httpContext.TraceIdentifier
        };

        string? correlationId = httpContext.Items.TryGetValue(
            CorrelationIdMiddleware.HttpContextItemKey, out object? correlationIdValue)
                ? correlationIdValue as string
                : null;

        if (!string.IsNullOrWhiteSpace(correlationId))
        {
            extensions[CorrelationIdExtensionName] = correlationId;
        }

        await TypedResults.Problem(
            type: ProblemTypeUris.ServerError,
            title: "Server error",
            statusCode: StatusCodes.Status500InternalServerError,
            detail: "An unexpected error occurred.",
            extensions: extensions)
            .ExecuteAsync(httpContext);

        return true;
    }
}
