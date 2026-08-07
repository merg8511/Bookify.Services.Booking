using Bookify.Services.Booking.Domain.Shared;
using Microsoft.AspNetCore.Http.HttpResults;
using System.Diagnostics;

namespace Bookify.Services.Booking.Api.Extensions;

internal static class ErrorHttpExtensions
{
    private const string CodeExtensionName = "code";
    private const string TraceIdExtensionName = "traceId";

    public static ProblemHttpResult ToProblem(
        this Error error,
        HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        int statusCode = GetStatusCode(error.Type);

        var extensions =
            new Dictionary<string, object?>
            {
                [CodeExtensionName] = error.Code,
                [TraceIdExtensionName] = GetTraceId(httpContext)
            };

        return TypedResults.Problem(
            type:
                GetProblemTypeUri(error.Type),
            title: GetTitle(error.Type),
            statusCode: statusCode,
            detail: GetDetail(
                error,
                statusCode),
            instance: GetInstance(httpContext),
            extensions: extensions);
    }

    private static int GetStatusCode(ErrorType errorType)
    {
        return errorType switch
        {
            ErrorType.Validation =>
                StatusCodes.Status400BadRequest,

            ErrorType.NotFound =>
                StatusCodes.Status404NotFound,

            ErrorType.Conflict =>
                StatusCodes.Status409Conflict,

            _ =>
                StatusCodes.Status500InternalServerError
        };
    }

    private static string GetProblemTypeUri(
        ErrorType errorType)
    {
        return errorType switch
        {
            ErrorType.Validation =>
                ProblemTypeUris.Validation,

            ErrorType.NotFound =>
                ProblemTypeUris.NotFound,

            _ =>
                ProblemTypeUris.ServerError
        };
    }

    private static string GetTitle(ErrorType errorType)
    {
        return errorType switch
        {
            ErrorType.Validation =>
                "Validation error",

            ErrorType.NotFound =>
                "Resource not found",

            _ =>
                "Server error"
        };
    }

    private static string GetDetail(
        Error error,
        int statusCode)
    {
        if (statusCode >=
            StatusCodes.Status500InternalServerError)
        {
            return "An unexpected error occurred.";
        }

        return error.Message;
    }

    private static string GetInstance(
        HttpContext httpContext)
    {
        string pathBase =
            httpContext.Request.PathBase.Value ??
            string.Empty;

        string path =
            httpContext.Request.Path.Value ??
            "/";

        return $"{pathBase}{path}";
    }

    private static string GetTraceId(
        HttpContext httpContext)
    {
        return Activity.Current?.Id ??
               httpContext.TraceIdentifier;
    }
}
