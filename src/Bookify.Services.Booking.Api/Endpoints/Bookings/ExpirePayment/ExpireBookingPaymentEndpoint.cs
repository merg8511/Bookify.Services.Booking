using Bookify.Services.Booking.Api.Extensions;
using Bookify.Services.Booking.Application.Abstractions.Messaging;
using Bookify.Services.Booking.Application.Bookings.ExpirePayment;
using Bookify.Services.Booking.Domain.Shared;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Bookify.Services.Booking.Api.Endpoints.Bookings.ExpirePayment;

internal static class ExpireBookingPaymentEndpoint
{
    public static void Map(
        RouteGroupBuilder bookingsGroup)
    {
        bookingsGroup
            .MapPost("/{bookingId:guid}/expire-payment",
                HandleAsync)
            .WithName(EndpointNames.Bookings.ExpirePayment)
            .WithSummary("Expires a pending booking payment.")
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
        ICommandExecutor<ExpireBookingPaymentCommand> commandExecutor,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var command = new ExpireBookingPaymentCommand(bookingId);

        Result result = await commandExecutor.ExecuteAsync(command, cancellationToken);

        if (result.IsFailure)
        {
            return result.Error
                .ToProblem(httpContext);
        }

        return TypedResults.NoContent();
    }
}
