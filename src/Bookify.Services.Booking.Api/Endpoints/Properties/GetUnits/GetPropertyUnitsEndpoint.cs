using Bookify.Services.Booking.Api.Extensions;
using Bookify.Services.Booking.Application.Abstractions.Messaging;
using Bookify.Services.Booking.Application.RentableUnits.GetByProperty;
using Bookify.Services.Booking.Application.RentableUnits.ReadModels;
using Bookify.Services.Booking.Domain.Shared;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Bookify.Services.Booking.Api.Endpoints.Properties.GetUnits;

internal static class GetPropertyUnitsEndpoint
{
    public static void Map(RouteGroupBuilder propertiesGroup)
    {
        propertiesGroup
            .MapGet("/{propertyId:guid}/units", HandleAsync)
            .WithName(EndpointNames.Properties.GetUnits)
            .WithSummary("Gets the active rentable units publicly available for a property.")
            .Produces<GetPropertyUnitsResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status500InternalServerError);
    }

    private static async Task<Results<Ok<GetPropertyUnitsResponse>, ProblemHttpResult>> HandleAsync(
        Guid propertyId,
        IQueryExecutor<GetPropertyUnitsQuery, IReadOnlyList<RentableUnitListItemReadModel>> queryExecutor,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var query = new GetPropertyUnitsQuery(propertyId);

        Result<IReadOnlyList<RentableUnitListItemReadModel>> result = await queryExecutor
            .ExecuteAsync(query, cancellationToken);

        return result.ToHttpResult(
            httpContext,
            units => TypedResults.Ok(MapToResponse(propertyId, units)));
    }

    private static GetPropertyUnitsResponse MapToResponse(
        Guid propertyId,
        IReadOnlyList<RentableUnitListItemReadModel> units)
    {
        PropertyRentableUnitResponse[] responseUnits = units
            .Select(unit => new PropertyRentableUnitResponse(
                unit.Id,
                unit.Name,
                unit.Type,
                unit.MaximumCapacity,
                unit.MaxBaseGuests,
                unit.IsEntireProperty))
            .ToArray();

        return new GetPropertyUnitsResponse(
            propertyId,
            responseUnits);
    }
}
