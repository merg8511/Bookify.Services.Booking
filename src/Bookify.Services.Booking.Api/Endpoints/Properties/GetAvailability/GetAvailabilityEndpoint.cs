using Bookify.Services.Booking.Api.Extensions;
using Bookify.Services.Booking.Application.Abstractions.Messaging;
using Bookify.Services.Booking.Application.Availability.Get;
using Bookify.Services.Booking.Application.Availability.ReadModels;
using Bookify.Services.Booking.Domain.Shared;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Bookify.Services.Booking.Api.Endpoints.Properties.GetAvailability;

internal static class GetAvailabilityEndpoint
{
    public static void Map(RouteGroupBuilder propertiesGroup)
    {
        propertiesGroup
            .MapGet(
                "/{propertyId:guid}/availability",
                HandleAsync)
            .WithName(
                EndpointNames.Properties.GetAvailability)
            .WithSummary(
                "Gets the rentable units available " +
                "for a requeted stay period and " +
                "guest count.")
            .Produces<GetAvailabilityResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status500InternalServerError);
    }

    private static async Task<Results<
        Ok<GetAvailabilityResponse>,
        ProblemHttpResult>> HandleAsync(
            Guid propertyId,
            DateOnly? checkInDate,
            DateOnly? checkOutDate,
            int? guestCount,
            IQueryExecutor<
                GetAvailabilityQuery,
                AvailabilityReadModel> queryExecutor,
            HttpContext httpContext,
            CancellationToken cancellationToken)
    {
        var query =
            new GetAvailabilityQuery(
                propertyId,
                checkInDate,
                checkOutDate,
                guestCount);

        Result<AvailabilityReadModel> result =
            await queryExecutor.ExecuteAsync(
                query,
                cancellationToken);

        return result.ToHttpResult(
            httpContext,
            availability =>
                TypedResults.Ok(
                    MapToResponse(availability)));
    }

    private static GetAvailabilityResponse MapToResponse(AvailabilityReadModel availability)
    {
        AvailableRentableUnitResponse[] units =
            availability.AvailableUnits
                .Select(
                    unit =>
                        new AvailableRentableUnitResponse(
                                unit.Id,
                                unit.Name,
                                unit.Type,
                                unit.MaximumCapacity,
                                unit.IsEntireProperty))
                .ToArray();

        return new GetAvailabilityResponse(
            availability.PropertyId,
            availability.PropertyName,
            availability.CheckInDate,
            availability.CheckOutDate,
            availability.NumberOfNights,
            availability.GuestCount,
            units);
    }
}
