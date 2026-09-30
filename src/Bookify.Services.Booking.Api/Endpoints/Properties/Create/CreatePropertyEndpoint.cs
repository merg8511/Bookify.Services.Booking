using Bookify.Services.Booking.Api.Extensions;
using Bookify.Services.Booking.Application.Abstractions.Messaging;
using Bookify.Services.Booking.Application.Abstractions.Security;
using Bookify.Services.Booking.Application.Properties.Create;
using Bookify.Services.Booking.Domain.Shared;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Bookify.Services.Booking.Api.Endpoints.Properties.Create;

internal static class CreatePropertyEndpoint
{
    public static void Map(RouteGroupBuilder propertiesGroup)
    {
        propertiesGroup
            .MapPost("/", HandleAsync)
            .WithName(EndpointNames.Properties.Create)
            .WithSummary("Creates a property.")
            .RequireAuthorization(policy => policy.RequireRole(
                BookifyRoles.Owner,
                BookifyRoles.Admin))
            .Accepts<CreatePropertyRequest>("application/json")
            .Produces<CreatePropertyResponse>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status415UnsupportedMediaType)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status500InternalServerError);
    }

    private static async Task<Results<CreatedAtRoute<CreatePropertyResponse>, ProblemHttpResult>> HandleAsync(
        CreatePropertyRequest request,
        ICommandExecutor<CreatePropertyCommand, Guid> executor,
        ICurrentUser currentUser,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        string ownerSubjectId = currentUser.Subject ??
            throw new InvalidOperationException("An authenticated property owner must have a subject identifier.");

        var command = new CreatePropertyCommand(
            request.Name,
            request.TimeZoneId,
            request.CheckInTime,
            request.CheckOutTime,
            ownerSubjectId);

        Result<Guid> result = await executor.ExecuteAsync(command, cancellationToken);

        return result.ToHttpResult(
            httpContext,
            propertyId =>
            {
                var response = new CreatePropertyResponse(propertyId);

                return TypedResults.CreatedAtRoute(
                    value: response,
                    routeName: EndpointNames.Properties.GetById,
                    routeValues: new
                    {
                        propertyId
                    });
            });
    }
}
