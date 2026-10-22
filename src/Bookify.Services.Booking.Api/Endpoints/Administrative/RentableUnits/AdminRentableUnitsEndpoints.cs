using Bookify.Services.Booking.Api.Endpoints.Administrative.RentableUnits.Pricing;
using Bookify.Services.Booking.Api.Endpoints.Administrative.RentableUnits.Seasons;
using Bookify.Services.Booking.Api.Extensions;
using Bookify.Services.Booking.Api.Security;
using Bookify.Services.Booking.Application.Abstractions.Messaging;
using Bookify.Services.Booking.Application.RentableUnits.Management.Create;
using Bookify.Services.Booking.Application.RentableUnits.Management.GetAll;
using Bookify.Services.Booking.Application.RentableUnits.Management.GetById;
using Bookify.Services.Booking.Application.RentableUnits.Management.SetStatus;
using Bookify.Services.Booking.Application.RentableUnits.Management.Update;
using Bookify.Services.Booking.Application.RentableUnits.ReadModels;
using Bookify.Services.Booking.Domain.Shared;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Bookify.Services.Booking.Api.Endpoints.Administrative.RentableUnits;

internal static class AdminRentableUnitsEndpoints
{

    public static void Map(RouteGroupBuilder apiGroup)
    {
        RouteGroupBuilder unitsGroup = apiGroup
            .MapGroup("/admin/properties/{propertyId:guid}/units")
            .WithTags("Admin RentableUnits");

        unitsGroup
            .MapPost("/", CreateAsync)
            .WithName(EndpointNames.AdminRentableUnits.Create)
            .WithSummary("Creates a rentable unit for a property.")
            .RequirePropertyOwnerOrAdminAccessFromRoute("propertyId")
            .Accepts<CreateRentableUnitRequest>("application/json")
            .Produces<CreateRentableUnitRequest>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status415UnsupportedMediaType)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        unitsGroup
            .MapGet("/", GetAllAsync)
            .WithName(EndpointNames.AdminRentableUnits.List)
            .WithSummary("List all rentable units, including inactive units.")
            .RequirePropertyOwnerOrAdminAccessFromRoute("propertyId")
            .Produces<CreateRentableUnitRequest>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        unitsGroup
            .MapGet("/{unitId:guid}", GetByIdAsync)
            .WithName(EndpointNames.AdminRentableUnits.GetById)
            .WithSummary("Gets an administrative rentable unit detail.")
            .RequirePropertyOwnerOrAdminAccessFromRoute("propertyId")
            .Produces<CreateRentableUnitRequest>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        unitsGroup
            .MapPut("/{unitId:guid}", UpdateAsync)
            .WithName(EndpointNames.AdminRentableUnits.Update)
            .WithSummary("Updates rentable unit detail and capacity.")
            .RequirePropertyOwnerOrAdminAccessFromRoute("propertyId")
            .Accepts<CreateRentableUnitRequest>("application/json")
            .Produces<CreateRentableUnitRequest>(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status415UnsupportedMediaType)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        unitsGroup
            .MapPatch("/{unitId:guid}/status", SetStatusAsync)
            .WithName(EndpointNames.AdminRentableUnits.SetStatus)
            .WithSummary("Activates or deactivates a rentable unit.")
            .RequirePropertyOwnerOrAdminAccessFromRoute("propertyId")
            .Accepts<CreateRentableUnitRequest>("application/json")
            .Produces<CreateRentableUnitRequest>(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status415UnsupportedMediaType)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        AdminRentableUnitPricingEndpoints.Map(unitsGroup);
        AdminPricingSeasonEndpoints.Map(unitsGroup);
    }

    private static async Task<Results<CreatedAtRoute<CreateRentableUnitResponse>, ProblemHttpResult>> CreateAsync(
        Guid propertyId,
        CreateRentableUnitRequest request,
        ICommandExecutor<CreateRentableUnitCommand, Guid> executor,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var command = new CreateRentableUnitCommand(
            propertyId,
            request.Name,
            request.Type,
            request.MaximumCapacity,
            request.MaxBaseGuests);

        Result<Guid> result = await executor.ExecuteAsync(command, cancellationToken);

        return result.ToHttpResult(
            httpContext,
            unitId => TypedResults.CreatedAtRoute(
                value: new CreateRentableUnitResponse(unitId),
                routeName: EndpointNames.AdminRentableUnits.GetById,
                routeValues: new
                {
                    propertyId,
                    unitId
                }));
    }

    private static async Task<Results<Ok<GetAdminRentableUnitsResponse>, ProblemHttpResult>> GetAllAsync(
        Guid propertyId,
        IQueryExecutor<GetAdminRentableUnitsQuery, IReadOnlyList<RentableUnitDetailsReadModel>> executor,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var query = new GetAdminRentableUnitsQuery(propertyId);

        Result<IReadOnlyList<RentableUnitDetailsReadModel>> result = await executor.ExecuteAsync(query, cancellationToken);

        return result.ToHttpResult(
            httpContext,
            units => TypedResults.Ok(
                new GetAdminRentableUnitsResponse(
                    propertyId,
                    units.Select(MapToResponse).ToArray())));
    }

    private static async Task<Results<Ok<AdminRentableUnitResponse>, ProblemHttpResult>> GetByIdAsync(
        Guid propertyId,
        Guid unitId,
        IQueryExecutor<GetAdminRentableUnitByIdQuery, RentableUnitDetailsReadModel> executor,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var query = new GetAdminRentableUnitByIdQuery(propertyId, unitId);

        Result<RentableUnitDetailsReadModel> result = await executor.ExecuteAsync(query, cancellationToken);

        return result.ToHttpResult(
            httpContext,
            unit => TypedResults.Ok(MapToResponse(unit)));
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> UpdateAsync(
        Guid propertyId,
        Guid unitId,
        UpdateRentableUnitRequest request,
        ICommandExecutor<UpdateRentableUnitCommand> executor,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var command = new UpdateRentableUnitCommand(
            propertyId,
            unitId,
            request.Name,
            request.Type,
            request.MaximumCapacity,
            request.MaxBaseGuests);

        Result result = await executor.ExecuteAsync(command, cancellationToken);

        if (result.IsFailure)
        {
            return result.Error.ToProblem(httpContext);
        }

        return TypedResults.NoContent();
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> SetStatusAsync(
        Guid propertyId,
        Guid unitId,
        SetRentableUnitStatusRequest request,
        ICommandExecutor<SetRentableUnitStatusCommand> executor,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var command = new SetRentableUnitStatusCommand(
            propertyId,
            unitId,
            request.IsActive);

        Result result = await executor.ExecuteAsync(command, cancellationToken);

        if (result.IsFailure)
        {
            return result.Error.ToProblem(httpContext);
        }

        return TypedResults.NoContent();
    }

    private static AdminRentableUnitResponse MapToResponse(RentableUnitDetailsReadModel unit)
    {
        return new AdminRentableUnitResponse(
            unit.Id,
            unit.PropertyId,
            unit.PropertyName,
            unit.Name,
            unit.Type,
            unit.MaximumCapacity,
            unit.MaxBaseGuests,
            unit.IsActive,
            unit.IsEntireProperty);
    }
}
