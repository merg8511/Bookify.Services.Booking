using Bookify.Services.Booking.Api.Extensions;
using Bookify.Services.Booking.Application.Abstractions.Idempotency;
using Bookify.Services.Booking.Application.Idempotency;
using Bookify.Services.Booking.Domain.Shared;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Primitives;
using System.Security.Cryptography;
using System.Text;

namespace Bookify.Services.Booking.Api.Idempotency;

internal sealed class IdempotencyMiddleware
{
    private const string HeaderName = "Idempotency-key";
    private const int MaximumKeyLength = 255;
    private readonly RequestDelegate _next;

    public IdempotencyMiddleware(RequestDelegate next)
    {
        _next = next ??
            throw new ArgumentNullException(nameof(next));
    }

    public async Task InvokeAsync(
        HttpContext httpContext,
        IIdempotencyProcessor idempotencyProcessor)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        ArgumentNullException.ThrowIfNull(idempotencyProcessor);

        if (!RequiresIdempotency(httpContext))
        {
            await _next(httpContext);
            return;
        }

        StringValues keyValues = GetIdempotencyKeyValues(httpContext.Request);

        if (keyValues.Count == 0)
        {
            await WriteProblemAsync(httpContext, IdempotencyHttpErrors.KeyRequired);
            return;
        }

        if (keyValues.Count != 1 ||
            string.IsNullOrWhiteSpace(
                keyValues[0]) ||
            keyValues[0]!.Length >
                MaximumKeyLength)
        {
            await WriteProblemAsync(httpContext, IdempotencyHttpErrors.InvalidKey);
            return;
        }

        string key = keyValues[0]!;

        string requestHash = await CalculateRequestHashAsync(
            httpContext.Request,
            httpContext.RequestAborted);

        string endpoint = GetEndpointScope(httpContext);

        var context =
            new IdempotencyRequestContext(
                key,
                httpContext.Request.Method.ToUpperInvariant(),
                endpoint,
                requestHash);

        Result<IdempotencyProcessingResult>
            processingResult =
                await idempotencyProcessor
                    .BeginAsync(
                        context,
                        httpContext.RequestAborted);

        if (processingResult.IsFailure)
        {
            await WriteProblemAsync(httpContext, processingResult.Error);
            return;
        }

        IdempotencyProcessingResult decision = processingResult.Value;

        if (decision.Action == IdempotencyProcessingAction.Replay)
        {
            await ReplayAsync(httpContext, decision);
            return;
        }

        await ExecuteAndCaptureAsync(httpContext, idempotencyProcessor, context);
    }

    private async Task ExecuteAndCaptureAsync(
        HttpContext httpContext,
        IIdempotencyProcessor idempotencyProcessor,
        IdempotencyRequestContext context)
    {
        Stream originalResponseBody = httpContext.Request.Body;

        await using var responseBuffer = new MemoryStream();

        httpContext.Response.Body = responseBuffer;

        try
        {
            await _next(httpContext);

            int statusCode = httpContext.Response.StatusCode;
            string? responseBody = GetResponseBody(responseBuffer);

            // At this point the business operation has
            // already completed. Persist the replay data
            // even if the client disconnects immediately.
            await idempotencyProcessor.CompleteAsync(
                context,
                statusCode,
                responseBody,
                CancellationToken.None);

            responseBuffer.Position = 0;

            await responseBuffer.CopyToAsync(originalResponseBody, httpContext.RequestAborted);
        }
        finally
        {
            httpContext.Response.Body = originalResponseBody;
        }
    }

    private static bool RequiresIdempotency(HttpContext httpContext)
    {
        return httpContext
            .GetEndpoint()?
            .Metadata
            .GetMetadata<IdempotencyRequiredMetadata>()
            is not null;
    }

    private static StringValues GetIdempotencyKeyValues(HttpRequest request)
    {
        return request.Headers[
            HeaderName];
    }

    private static async Task<string>
        CalculateRequestHashAsync(
        HttpRequest request,
        CancellationToken cancellationToken)
    {
        request.EnableBuffering();

        request.Body.Position = 0;

        byte[] hash = await SHA256.HashDataAsync(request.Body, cancellationToken);

        request.Body.Position = 0;

        return Convert.ToHexString(hash);
    }

    private static string GetEndpointScope(HttpContext httpContext)
    {
        if (httpContext.GetEndpoint() is not RouteEndpoint routeEndpoint)
        {
            throw new InvalidOperationException("Idempotency requires a routed endpoint.");
        }

        string? rawPattern =
            routeEndpoint
                .RoutePattern
                .RawText;

        if (string.IsNullOrWhiteSpace(rawPattern))
        {
            throw new InvalidOperationException(
                "The routed endpoint does not expose " +
                "a route pattern.");
        }

        string normalized =
            rawPattern.StartsWith('/')
                ? rawPattern
                : $"/{rawPattern}";

        return normalized;
    }

    private static string? GetResponseBody(MemoryStream responseBuffer)
    {
        if (responseBuffer.Length == 0)
        {
            return null;
        }

        return Encoding.UTF8.GetString(responseBuffer.ToArray());
    }

    private static async Task ReplayAsync(
        HttpContext httpContext,
        IdempotencyProcessingResult result)
    {
        int statusCode = result.StatusCode ??
            throw new InvalidOperationException(
                "A replay result must contain " +
                "an HTTP status code.");

        if (result.ResponseBody is null)
        {
            return;
        }

        httpContext.Response.ContentType = GetReplayContentType(statusCode);

        await httpContext.Response
            .WriteAsync(
                result.ResponseBody,
                httpContext.RequestAborted);
    }

    private static string GetReplayContentType(int statusCode)
    {
        return statusCode >= 400
            ? "application/problem+json"
            : "application/json";
    }

    private static async Task WriteProblemAsync(HttpContext httpContext, Error error)
    {
        ProblemHttpResult problem =
            error.ToProblem(httpContext);

        await problem.ExecuteAsync(httpContext);
    }
}
