using Bookify.Services.Booking.Api.Extensions;
using Bookify.Services.Booking.Application.Abstractions.Messaging;
using Bookify.Services.Booking.Application.Bookings.Approve;
using Bookify.Services.Booking.Domain.Shared;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Bookify.Services.Booking.Api.Endpoints.Bookings.Approve;

internal static class ApproveBookingEndpoint
{
    public static void Map(
        RouteGroupBuilder bookingGroup)
    {
        bookingGroup
            .MapPost("/{bookingId:guid}/approve",
                HandleAsync)
            .WithName(EndpointNames.Bookings.Approve)
            .WithSummary("Approves a pending booking.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status500InternalServerError);
    }

    private static async Task<
        Results<
            NoContent,
            ProblemHttpResult>> HandleAsync(
        Guid bookingId,
        ICommandExecutor<ApproveBookingCommand> commandExecutor,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var command = new ApproveBookingCommand(bookingId);

        Result result = await commandExecutor.ExecuteAsync(command, cancellationToken);

        if (result.IsFailure)
        {
            return result.Error
                .ToProblem(httpContext);
        }

        return TypedResults.NoContent();
    }
}
