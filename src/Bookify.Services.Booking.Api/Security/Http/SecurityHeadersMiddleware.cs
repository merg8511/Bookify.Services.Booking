using Scalar.AspNetCore;

namespace Bookify.Services.Booking.Api.Security.Http;

internal sealed class SecurityHeadersMiddleware
{
    private const string ApiContentSecurityPolicy = "default-src 'none'; frame-ancestors 'none'; base-uri 'none'";
    private readonly RequestDelegate _next;

    public SecurityHeadersMiddleware(RequestDelegate next)
    {
        _next = next ??
            throw new ArgumentNullException(nameof(next));
    }

    public async Task InvokeAsync(HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        httpContext.Response.OnStarting(() =>
        {
            AddSecurityHeaders(httpContext);
            return Task.CompletedTask;
        });

        await _next(httpContext);
    }

    private static void AddSecurityHeaders(HttpContext httpContext)
    {
        IHeaderDictionary headers = httpContext.Response.Headers;

        headers.TryAdd("X-Content-Type-Options", "nosniff");
        headers.TryAdd("X-Frame-Options", "DENY");
        headers.TryAdd("Referrer-Policy", "no-referrer");
        headers.TryAdd("Permissions-Policy", "camera=(), microphone=(), geolocation=()");

        if (httpContext.Items.TryGetValue(ScalarOptions.NonceHttpContextItemKey, out object? nonceValue) &&
            nonceValue is string nonce &&
            !string.IsNullOrWhiteSpace(nonce))
        {
            string scalarPolicy =
                "default-src 'none'; " +
                $"script-src 'self' 'nonce-{nonce}'; " +
                "style-src 'self' 'unsafe-inline'; " +
                "connect-src 'self'; " +
                "img-src 'self' data:; " +
                "font-src 'self'; " +
                "worker-src 'self' blob:; " +
                "object-src 'none'; " +
                "frame-ancestors 'none'; " +
                "base-uri 'none'";

            headers.TryAdd("Content-Security-Policy", scalarPolicy);
        }
        else
        {
            headers.TryAdd("Content-Security-Policy", ApiContentSecurityPolicy);
        }

        EndpointAccessMetadata? accessMetadata = httpContext.GetEndpoint()?.Metadata.GetMetadata<EndpointAccessMetadata>();

        if (accessMetadata?.Category == EndpointAccessCategory.CustomerOrGuest)
        {
            headers["Cache-Control"] = "no-store";
            headers["Pragma"] = "no-cache";
        }
    }
}
