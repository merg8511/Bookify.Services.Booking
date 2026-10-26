using Bookify.Services.Booking.Api.Extensions;
using Bookify.Services.Booking.Api.Security;
using Bookify.Services.Booking.Application.Abstractions.Messaging;
using Bookify.Services.Booking.Application.Properties.Update;
using Bookify.Services.Booking.Domain.Shared;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Bookify.Services.Booking.Api.Endpoints.Properties.Update;

internal static class UpdatePropertyEndpoint
{
    public static void Map(RouteGroupBuilder propertiesGroup)
    {
        propertiesGroup
            .MapPut("/{propertyId:guid}", HandleAsync)
            .WithName(EndpointNames.Properties.Update)
            .WithSummary("Updates property details and stay schedules.")
            .RequirePropertyOwnerOrAdminAccessFromRoute("propertyId")
            .Accepts<UpdatePropertyRequest>("application/json")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status415UnsupportedMediaType)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status500InternalServerError);
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> HandleAsync(
        Guid propertyId,
        UpdatePropertyRequest request,
        ICommandExecutor<UpdatePropertyCommand> executor,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var command = new UpdatePropertyCommand(
            propertyId,
            request.Name,
            request.TimeZoneId,
            request.CheckInTime,
            request.CheckOutTime);

        Result result = await executor.ExecuteAsync(command, cancellationToken);

        if (result.IsFailure)
        {
            return result.Error.ToProblem(httpContext);
        }

        return TypedResults.NoContent();
    }
}
