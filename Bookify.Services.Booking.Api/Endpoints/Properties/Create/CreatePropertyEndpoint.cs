using Bookify.Services.Booking.Api.Extensions;
using Bookify.Services.Booking.Application.Abstractions.Messaging;
using Bookify.Services.Booking.Application.Properties.Create;
using Bookify.Services.Booking.Domain.Shared;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Bookify.Services.Booking.Api.Endpoints.Properties.Create;

internal static class CreatePropertyEndpoint
{
    public static void Map(
        RouteGroupBuilder propertiesGroup)
    {
        propertiesGroup
            .MapPost(
                "/",
                HandleAsync)
            .WithName(
                EndpointNames.Properties.Create)
            .WithSummary(
                "Creates a property.")
            .Produces<CreatePropertyResponse>(
                StatusCodes.Status201Created)
            .ProducesProblem(
                StatusCodes.Status400BadRequest);
    }

    private static async Task<
        Results<
            CreatedAtRoute<CreatePropertyResponse>,
            ProblemHttpResult>> HandleAsync(
        CreatePropertyRequest request,
        ICommandExecutor<
            CreatePropertyCommand,
            Guid> executor,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var command =
            new CreatePropertyCommand(
                request.Name,
                request.TimeZoneId,
                request.CheckInTime,
                request.CheckOutTime);

        Result<Guid> result =
            await executor.ExecuteAsync(
                command,
                cancellationToken);

        if (result.IsFailure)
        {
            return result.Error.ToProblem(
                httpContext);
        }

        var response = new CreatePropertyResponse(result.Value);

        return TypedResults.CreatedAtRoute(
            response,
            EndpointNames.Properties.GetById,
            new
            {
                propertyId = result.Value
            });
    }
}
