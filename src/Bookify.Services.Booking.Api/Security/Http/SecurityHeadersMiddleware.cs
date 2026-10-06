namespace Bookify.Services.Booking.Api.Security.Http;

internal sealed class SecurityHeadersMiddleware
{
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
        headers.TryAdd("Content-Security-Policy", "default-src 'none'; frame-ancestors 'none'; base-uri 'none'");

        EndpointAccessMetadata? accessMetadata = httpContext.GetEndpoint()?.Metadata.GetMetadata<EndpointAccessMetadata>();

        if (accessMetadata?.Category == EndpointAccessCategory.CustomerOrGuest)
        {
            headers["Cache-Control"] = "no-store";
            headers["Pragma"] = "no-cache";
        }
    }
}
