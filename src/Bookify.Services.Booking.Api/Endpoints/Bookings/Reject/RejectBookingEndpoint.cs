using Bookify.Services.Booking.Api.Extensions;
using Bookify.Services.Booking.Application.Abstractions.Messaging;
using Bookify.Services.Booking.Application.Bookings.Reject;
using Bookify.Services.Booking.Domain.Shared;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Bookify.Services.Booking.Api.Endpoints.Bookings.Reject;

internal static class RejectBookingEndpoint
{

    public static void Map(
        RouteGroupBuilder bookingsGroup)
    {
        bookingsGroup
            .MapPost("/{bookingId:guid}/reject",
                HandleAsync)
            .WithName(EndpointNames.Bookings.Reject)
            .WithSummary("Rejects a pending booking.")
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
        ICommandExecutor<RejectBookingCommand> commandExecutor,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var command = new RejectBookingCommand(bookingId);

        Result result =
            await commandExecutor
                .ExecuteAsync(
                    command,
                    cancellationToken);

        if (result.IsFailure)
        {
            return result.Error
                .ToProblem(httpContext);
        }

        return TypedResults.NoContent();
    }
}
