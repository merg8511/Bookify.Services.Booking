using Bookify.Services.Booking.Api.Extensions;
using Bookify.Services.Booking.Api.Security;
using Bookify.Services.Booking.Application.Abstractions.Messaging;
using Bookify.Services.Booking.Application.RentableUnits.Management.Seasons;
using Bookify.Services.Booking.Application.RentableUnits.Management.Seasons.Create;
using Bookify.Services.Booking.Application.RentableUnits.Management.Seasons.GetAll;
using Bookify.Services.Booking.Application.RentableUnits.Management.Seasons.GetById;
using Bookify.Services.Booking.Application.RentableUnits.Management.Seasons.SetStatus;
using Bookify.Services.Booking.Application.RentableUnits.Management.Seasons.Update;
using Bookify.Services.Booking.Domain.Shared;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Bookify.Services.Booking.Api.Endpoints.Administrative.RentableUnits.Seasons;

internal static class AdminPricingSeasonEndpoints
{
    public static void Map(RouteGroupBuilder unitsGroup)
    {
        unitsGroup
            .MapPost("/{unitId:guid}/seasons", CreateAsync)
            .WithName(EndpointNames.AdminRentableUnits.CreateSeason)
            .WithSummary("Creates a pricing season for a rentable unit.")
            .RequirePropertyOwnerOrAdminAccessFromRoute("propertyId")
            .Accepts<CreatePricingSeasonRequest>("application/json")
            .Produces<CreatePricingSeasonResponse>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status415UnsupportedMediaType)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        unitsGroup
            .MapGet("/{unitId:guid}/seasons", GetAllAsync)
            .WithName(EndpointNames.AdminRentableUnits.ListSeason)
            .WithSummary("Lists active and inactive pricing seasons.")
            .RequirePropertyOwnerOrAdminAccessFromRoute("propertyId")
            .Produces<GetAdminPricingSeasonsResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        unitsGroup
            .MapGet("/{unitId:guid}/seasons/{seasonId:int}", GetByIdAsync)
            .WithName(EndpointNames.AdminRentableUnits.GetSeasonById)
            .WithSummary("Gets a pricing season by its identifier.")
            .RequirePropertyOwnerOrAdminAccessFromRoute("propertyId")
            .Produces<AdminPricingSeasonResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        unitsGroup
            .MapPut("/{unitId:guid}/seasons/{seasonId:int}", UpdateAsync)
            .WithName(EndpointNames.AdminRentableUnits.UpdateSeason)
            .WithSummary("Updates pricing season dates, rate and priority")
            .RequirePropertyOwnerOrAdminAccessFromRoute("propertyId")
            .Accepts<UpdatePricingSeasonRequest>("application/json")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status415UnsupportedMediaType)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        unitsGroup
            .MapPatch("/{unitId:guid}/seasons/{seasonId:int}/status", SetStatusAsync)
            .WithName(EndpointNames.AdminRentableUnits.SetSeasonStatus)
            .WithSummary("Activates or deactivates a pricing season.")
            .RequirePropertyOwnerOrAdminAccessFromRoute("propertyId")
            .Accepts<SetPricingSeasonStatusRequest>("application/json")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status415UnsupportedMediaType)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

    }

    private static async Task<Results<CreatedAtRoute<
        CreatePricingSeasonResponse>, ProblemHttpResult>> CreateAsync(
        Guid propertyId,
        Guid unitId,
        CreatePricingSeasonRequest request,
        ICommandExecutor<CreatePricingSeasonCommand, int> executor,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var command = new CreatePricingSeasonCommand(
            propertyId,
            unitId,
            request.StartDate,
            request.EndDate,
            request.NightlyRate,
            request.Currency,
            request.Priority);

        Result<int> result = await executor.ExecuteAsync(command, cancellationToken);

        return result.ToHttpResult(
            httpContext,
            seasonId => TypedResults.CreatedAtRoute(
                value: new CreatePricingSeasonResponse(seasonId),
                routeName: EndpointNames.AdminRentableUnits.GetSeasonById,
                routeValues: new
                {
                    propertyId,
                    unitId,
                    seasonId
                }));
    }

    private static async Task<Results<Ok<GetAdminPricingSeasonsResponse>, ProblemHttpResult>> GetAllAsync(
        Guid propertyId,
        Guid unitId,
        IQueryExecutor<GetPricingSeasonsQuery, IReadOnlyList<PricingSeasonReadModel>> executor,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var query = new GetPricingSeasonsQuery(propertyId, unitId);

        Result<IReadOnlyList<PricingSeasonReadModel>> result = await executor
            .ExecuteAsync(query, cancellationToken);

        return result.ToHttpResult(
            httpContext,
            seasons => TypedResults.Ok(new GetAdminPricingSeasonsResponse(
                propertyId,
                unitId,
                seasons.Select(MapToResponse).ToArray())));
    }

    private static async Task<Results<Ok<AdminPricingSeasonResponse>, ProblemHttpResult>> GetByIdAsync(
        Guid propertyId,
        Guid unitId,
        int seasonId,
        IQueryExecutor<GetPricingSeasonByIdQuery, PricingSeasonReadModel> executor,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var query = new GetPricingSeasonByIdQuery(propertyId, unitId, seasonId);

        Result<PricingSeasonReadModel> result = await executor.ExecuteAsync(query, cancellationToken);

        return result.ToHttpResult(
            httpContext,
            season => TypedResults.Ok(MapToResponse(season)));
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> UpdateAsync(
        Guid propertyId,
        Guid unitId,
        int seasonId,
        UpdatePricingSeasonRequest request,
        ICommandExecutor<UpdatePricingSeasonCommand> executor,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var command = new UpdatePricingSeasonCommand(
            propertyId,
            unitId,
            seasonId,
            request.StartDate,
            request.EndDate,
            request.NightlyRate,
            request.Currency,
            request.Priority);

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
        int seasonId,
        SetPricingSeasonStatusRequest request,
        ICommandExecutor<SetPricingSeasonStatusCommand> executor,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var command = new SetPricingSeasonStatusCommand(
            propertyId,
            unitId,
            seasonId,
            request.IsActive);

        Result result = await executor.ExecuteAsync(command, cancellationToken);

        if (result.IsFailure)
        {
            return result.Error.ToProblem(httpContext);
        }

        return TypedResults.NoContent();
    }

    private static AdminPricingSeasonResponse MapToResponse(PricingSeasonReadModel season)
    {
        return new AdminPricingSeasonResponse(
            season.Id,
            season.StartDate,
            season.EndDate,
            season.NightlyRate,
            season.Currency,
            season.Priority,
            season.IsActive);
    }
}
