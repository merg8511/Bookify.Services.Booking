using Bookify.Services.Booking.Api.Extensions;
using Bookify.Services.Booking.Api.Security;
using Bookify.Services.Booking.Application.Abstractions.Messaging;
using Bookify.Services.Booking.Application.Properties.SetStatus;
using Bookify.Services.Booking.Domain.Shared;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Bookify.Services.Booking.Api.Endpoints.Properties.SetStatus;

internal static class SetPropertyStatusEndpoint
{
    public static void Map(RouteGroupBuilder propertiesGroup)
    {
        propertiesGroup
            .MapPatch("/{propertyId:guid}/status", HandleAsync)
            .WithName(EndpointNames.Properties.SetStatus)
            .WithSummary("Activates or deactivates a property.")
            .RequirePropertyOwnerOrAdminAccessFromRoute("propertyId")
            .Accepts<SetPropertyStatusRequest>("application/json")
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
        SetPropertyStatusRequest request,
        ICommandExecutor<SetPropertyStatusCommand> executor,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var command = new SetPropertyStatusCommand(propertyId, request.IsActive);

        Result result = await executor.ExecuteAsync(command, cancellationToken);

        if (result.IsFailure)
        {
            return result.Error.ToProblem(httpContext);
        }

        return TypedResults.NoContent();
    }
}
