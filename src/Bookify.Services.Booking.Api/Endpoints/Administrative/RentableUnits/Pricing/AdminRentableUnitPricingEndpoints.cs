using Bookify.Services.Booking.Api.Extensions;
using Bookify.Services.Booking.Api.Security;
using Bookify.Services.Booking.Application.Abstractions.Messaging;
using Bookify.Services.Booking.Application.RentableUnits.Management.Pricing;
using Bookify.Services.Booking.Application.RentableUnits.Management.Pricing.Configure;
using Bookify.Services.Booking.Application.RentableUnits.Management.Pricing.Get;
using Bookify.Services.Booking.Domain.Shared;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Bookify.Services.Booking.Api.Endpoints.Administrative.RentableUnits.Pricing;

internal static class AdminRentableUnitPricingEndpoints
{
    public static void Map(RouteGroupBuilder unitsGroup)
    {
        unitsGroup
            .MapGet("/{unitId:guid}/pricing", GetAsync)
            .WithName(EndpointNames.AdminRentableUnits.GetPricing)
            .WithSummary("Gets the base pricing configuration of a rentable unit.")
            .RequirePropertyOwnerOrAdminAccessFromRoute("propertyId")
            .Produces<GetAdminRentableUnitPricingResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        unitsGroup
            .MapPut("/{unitId:guid}/pricing", ConfigureAsyc)
            .WithName(EndpointNames.AdminRentableUnits.ConfigurePricing)
            .WithSummary("Configures the base pricing of a rentable unit.")
            .RequirePropertyOwnerOrAdminAccessFromRoute("propertyId")
            .Accepts<ConfigureRentableUnitPricingRequest>("application/json")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status415UnsupportedMediaType)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status500InternalServerError);
    }

    private static async Task<Results<Ok<GetAdminRentableUnitPricingResponse>, ProblemHttpResult>> GetAsync(
        Guid propertyId,
        Guid unitId,
        IQueryExecutor<GetAdminRentableUnitPricingQuery, AdminRentableUnitPricingReadModel> executor,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var query = new GetAdminRentableUnitPricingQuery(propertyId, unitId);

        Result<AdminRentableUnitPricingReadModel> result = await executor.ExecuteAsync(query, cancellationToken);

        return result.ToHttpResult(
            httpContext,
            pricing => TypedResults.Ok(MapToResponse(pricing)));
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> ConfigureAsyc(
        Guid propertyId,
        Guid unitId,
        ConfigureRentableUnitPricingRequest request,
        ICommandExecutor<ConfigureRentableUnitPricingCommand> executor,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var command = new ConfigureRentableUnitPricingCommand(
            propertyId,
            unitId,
            request.RegularNightlyRate,
            request.WeekendNightlyRate,
            request.ExtraGuestNightlyRate,
            request.Currency);

        Result result = await executor.ExecuteAsync(command, cancellationToken);

        if (result.IsFailure)
        {
            return result.Error.ToProblem(httpContext);
        }

        return TypedResults.NoContent();
    }

    private static GetAdminRentableUnitPricingResponse MapToResponse(
        AdminRentableUnitPricingReadModel model)
    {
        RentableUnitPricingReadModel? pricing = model.Pricing;

        return new GetAdminRentableUnitPricingResponse(
            model.PropertyId,
            model.UnitId,
            pricing is not null,
            pricing?.RegularNightlyRate,
            pricing?.WeekendNightlyRate,
            pricing?.ExtraGuestNightlyRate,
            pricing?.Currency);
    }
}
