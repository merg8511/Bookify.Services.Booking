using Bookify.Services.Booking.Api.Extensions;
using Bookify.Services.Booking.Application.Abstractions.Messaging;
using Bookify.Services.Booking.Application.Properties.GetById;
using Bookify.Services.Booking.Domain.Shared;
using Microsoft.AspNetCore.Http.HttpResults;
using ApplicationPropertyResponse =
    Bookify.Services.Booking.Application.Properties.GetById.PropertyResponse;

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
            .Produces<GetPropertyByIdResponse>(
                StatusCodes.Status200OK)
            .ProducesProblem(
                StatusCodes.Status400BadRequest)
            .ProducesProblem(
                StatusCodes.Status404NotFound)
            .ProducesProblem(
                StatusCodes.Status500InternalServerError);
    }

    private static async Task<
        Results<
            Ok<GetPropertyByIdResponse>,
            ProblemHttpResult>> HandleAsync(
        Guid propertyId,
        IQueryExecutor<
            GetPropertyByIdQuery,
            ApplicationPropertyResponse> executor,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var query = new GetPropertyByIdQuery(propertyId);

        Result<ApplicationPropertyResponse> result =
            await executor.ExecuteAsync(query, cancellationToken);

        return result.ToHttpResult(
            httpContext,
            property =>
                TypedResults.Ok(MapToResponse(property)));
    }

    private static GetPropertyByIdResponse MapToResponse(
        ApplicationPropertyResponse property)
    {
        return new GetPropertyByIdResponse(
            property.Id,
            property.Name,
            property.TimeZoneId,
            property.CheckInTime,
            property.CheckOutTime,
            property.IsActive);
    }
}
