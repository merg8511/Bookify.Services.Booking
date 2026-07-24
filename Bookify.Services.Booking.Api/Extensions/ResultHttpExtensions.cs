using Bookify.Services.Booking.Domain.Shared;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Bookify.Services.Booking.Api.Extensions;

internal static class ResultHttpExtensions
{
    public static Results<
        TSuccess,
        ProblemHttpResult> ToHttpResult<
        TValue,
        TSuccess>(
        this Result<TValue> result,
        HttpContext httpContext,
        Func<TValue, TSuccess> onSuccess)
        where TSuccess : IResult
    {
        ArgumentNullException.ThrowIfNull(
            httpContext);

        ArgumentNullException.ThrowIfNull(
            onSuccess);

        if (result.IsFailure)
        {
            return result.Error.ToProblem(
                httpContext);
        }

        return onSuccess(
            result.Value);
    }
}

