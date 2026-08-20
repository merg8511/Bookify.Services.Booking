using Bookify.Services.Booking.Api.Extensions;
using Bookify.Services.Booking.Application.Abstractions.Messaging;
using Bookify.Services.Booking.Application.Bookings.MarkAsPaid;
using Bookify.Services.Booking.Domain.Shared;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Bookify.Services.Booking.Api.Endpoints.Bookings.MarkAsPaid;

internal static class MarkBookingAsPaidEndpoint
{
    public static void Map(RouteGroupBuilder bookingsGroup)
    {
        bookingsGroup
            .MapPost("/{bookingId:guid}/mark-as-paid",
            HandleAsync)
            .WithName(EndpointNames.Bookings.MarkAsPaid)
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
        ICommandExecutor<MarkBookingAsPaidCommand> commandExecutor,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var command = new MarkBookingAsPaidCommand(bookingId);

        Result result = await commandExecutor.ExecuteAsync(command, cancellationToken);

        if (result.IsFailure)
        {
            return result.Error
                .ToProblem(httpContext);
        }

        return TypedResults.NoContent();
    }
}
