using Bookify.Services.Booking.Api.Extensions;
using Bookify.Services.Booking.Application.Abstractions.Messaging;
using Bookify.Services.Booking.Application.Properties.GetById;
using Bookify.Services.Booking.Domain.Shared;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Bookify.Services.Booking.Api.Endpoints.Properties.GetById;

internal static class GetPropertyByIdEndpoint
{
    public static void Map(RouteGroupBuilder propertiesGroup)
    {
        propertiesGroup
            .MapGet(
                "/{propertyId:guid}",
                HandleAsync)
            .WithName(
                EndpointNames.Properties.GetById)
            .WithSummary(
                "Gets a property by its identifier.")
            .Produces<PropertyResponse>(
                StatusCodes.Status200OK)
            .ProducesProblem(
                StatusCodes.Status400BadRequest)
            .ProducesProblem(
                StatusCodes.Status404NotFound);
    }

    private static async Task<
        Results<
            Ok<PropertyResponse>,
            ProblemHttpResult>> HandleAsync(
        Guid propertyId,
        IQueryExecutor<
            GetPropertyByIdQuery,
            PropertyResponse> executor,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var query = new GetPropertyByIdQuery(propertyId);

        Result<PropertyResponse> result =
            await executor.ExecuteAsync(query, cancellationToken);

        if (result.IsFailure)
        {
            return result.Error.ToProblem(httpContext);
        }

        return TypedResults.Ok(
            result.Value);
    }
}
