using Bookify.Services.Booking.Domain.Shared;
using Microsoft.AspNetCore.Http.HttpResults;
using System.Diagnostics;

namespace Bookify.Services.Booking.Api.Extensions;

internal static class ErrorHttpExtensions
{
    public static ProblemHttpResult ToProblem(
        this Error error,
        HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        int statusCode = GetStatusCode(error.Type);

        string detail =
            statusCode >=
                StatusCodes.Status500InternalServerError
                ? "An unexpected error ocurred."
                : error.Message;

        var extensions =
            new Dictionary<string, object?>
            {
                ["code"] = error.Code,
                ["traceId"] =
                    Activity.Current?.Id ??
                    httpContext.TraceIdentifier
            };

        return TypedResults.Problem(
            statusCode: statusCode,
            title: GetTitle(error.Type),
            detail: detail,
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

            _ =>
                StatusCodes.Status500InternalServerError
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
}
