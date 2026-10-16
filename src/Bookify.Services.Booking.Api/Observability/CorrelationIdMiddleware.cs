using Microsoft.Extensions.Primitives;

namespace Bookify.Services.Booking.Api.Observability;

internal sealed class CorrelationIdMiddleware
{
    internal static readonly object HttpContextItemKey = new();
    public const string HeaderName = "X-Correlation-ID";
    private const int MaximumLength = 64;
    private readonly RequestDelegate _next;
    private readonly ILogger<CorrelationIdMiddleware> _logger;

    public CorrelationIdMiddleware(
        RequestDelegate next,
        ILogger<CorrelationIdMiddleware> logger)
    {
        _next = next ??
            throw new ArgumentNullException(nameof(next));

        _logger = logger ??
            throw new ArgumentNullException(nameof(logger));
    }

    public async Task InvokeAsync(HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        string correlationId = ResolveCorrelationId(httpContext.Request.Headers);

        httpContext.Items[HttpContextItemKey] = correlationId;
        httpContext.Response.Headers[HeaderName] = correlationId;

        httpContext.Response.OnStarting(
            () =>
            {
                httpContext.Response.Headers[HeaderName] = correlationId;

                return Task.CompletedTask;
            });

        using (_logger.BeginScope(new Dictionary<string, object>
        {
            ["CorrelationId"] = correlationId
        }))
        {
            await _next(httpContext);
        }
    }

    private static string ResolveCorrelationId(IHeaderDictionary headers)
    {
        if (!headers.TryGetValue(HeaderName, out StringValues values))
        {
            return CreateCorrelationId();
        }

        if (values.Count != 1)
        {
            return CreateCorrelationId();
        }

        string? candidate = values[0];

        if (!IsValid(candidate))
        {
            return CreateCorrelationId();
        }

        return candidate!;
    }

    private static bool IsValid(string? value)
    {
        if (string.IsNullOrEmpty(value) ||
            value.Length > MaximumLength)
        {
            return false;
        }

        foreach (char character in value)
        {
            bool allowed = character is >= 'a' and <= 'z' or
                                        >= 'A' and <= 'Z' or
                                        >= '0' and <= '9' or
                                        '-' or '_' or '.';

            if (!allowed)
            {
                return false;
            }

        }
        return true;
    }

    private static string CreateCorrelationId()
    {
        return Guid.NewGuid().ToString("N");
    }
}
