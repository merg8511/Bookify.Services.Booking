using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.RateLimiting;
using System.Diagnostics;
using System.Globalization;
using System.Threading.RateLimiting;

namespace Bookify.Services.Booking.Api.RateLimiting;

internal static class RateLimitingProblemWriter
{
    private const string ProblemType = "urn:bookify:problem-type:rate-limit";
    private const string ErrorCode = "RateLimit.Exceeded";

    public static async ValueTask WriteAsync(
        OnRejectedContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        HttpContext httpContext = context.HttpContext;

        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out TimeSpan retryAfter))
        {
            int retryAfterSeconds = Math.Max(1, (int)Math.Ceiling(retryAfter.TotalSeconds));

            httpContext.Response.Headers.RetryAfter = retryAfterSeconds.ToString(CultureInfo.InvariantCulture);
        }

        var extensions = new Dictionary<string, object?>
        {
            ["code"] = ErrorCode,
            ["traceId"] = Activity.Current?.Id ?? httpContext.TraceIdentifier
        };

        ProblemHttpResult problem = TypedResults.Problem(
            type: ProblemType,
            title: "Too many requests",
            statusCode: StatusCodes.Status429TooManyRequests,
            detail: "The request rate limit has been exeeded. Try again later.",
            instance: GetInstance(httpContext),
            extensions: extensions);

        await problem.ExecuteAsync(httpContext);
    }

    private static string GetInstance(HttpContext httpContext)
    {
        string pathBase = httpContext.Request.PathBase.Value ?? string.Empty;
        string path = httpContext.Request.Path.Value ?? "/";

        return $"{pathBase}{path}";
    }
}
