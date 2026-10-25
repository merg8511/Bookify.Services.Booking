using Bookify.Services.Booking.Api.Extensions;
using Bookify.Services.Booking.Api.Security;
using Bookify.Services.Booking.Application.Abstractions.Messaging;
using Bookify.Services.Booking.Application.Bookings.Administrative.GetById;
using Bookify.Services.Booking.Application.Bookings.Approve;
using Bookify.Services.Booking.Application.Bookings.ReadModels;
using Bookify.Services.Booking.Application.Bookings.Reject;
using Bookify.Services.Booking.Domain.Shared;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Bookify.Services.Booking.Api.Endpoints.Administrative.Bookings.Actions;

internal static class AdminBookingActionsEndpoints
{
    public static void Map(RouteGroupBuilder bookingsGroup)
    {
        ArgumentNullException.ThrowIfNull(bookingsGroup);

        bookingsGroup
            .MapPost("/{bookingId:guid}/approve", ApproveAsync)
            .WithName(EndpointNames.AdminBookings.Approve)
            .WithSummary("Approves a pending booking within the specified property.")
            .RequirePropertyOwnerOrAdminAccessFromRoute("propertyId")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        bookingsGroup
            .MapPost("/{bookingId:guid}/reject", RejectAsync)
            .WithName(EndpointNames.AdminBookings.Reject)
            .WithSummary("Rejects a pending booking within the specified property.")
            .RequirePropertyOwnerOrAdminAccessFromRoute("propertyId")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status500InternalServerError);
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> ApproveAsync(
        Guid propertyId,
        Guid bookingId,
        IQueryExecutor<GetAdminBookingByIdQuery, BookingDetailsReadModel> bookingQueryExecutor,
        ICommandExecutor<ApproveBookingCommand> commandExecutor,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        Result<BookingDetailsReadModel> bookingResult = await bookingQueryExecutor.ExecuteAsync(
            new GetAdminBookingByIdQuery(propertyId, bookingId),
            cancellationToken);

        if (bookingResult.IsFailure)
        {
            return bookingResult.Error.ToProblem(httpContext);
        }

        Result result = await commandExecutor.ExecuteAsync(new ApproveBookingCommand(bookingId), cancellationToken);

        if (result.IsFailure)
        {
            return result.Error.ToProblem(httpContext);
        }

        return TypedResults.NoContent();
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> RejectAsync(
        Guid propertyId,
        Guid bookingId,
        IQueryExecutor<GetAdminBookingByIdQuery, BookingDetailsReadModel> bookingQueryExecutor,
        ICommandExecutor<RejectBookingCommand> commandExecutor,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        Result<BookingDetailsReadModel> bookingResult = await bookingQueryExecutor.ExecuteAsync(
            new GetAdminBookingByIdQuery(propertyId, bookingId),
            cancellationToken);

        if (bookingResult.IsFailure)
        {
            return bookingResult.Error.ToProblem(httpContext);
        }

        Result result = await commandExecutor.ExecuteAsync(new RejectBookingCommand(bookingId), cancellationToken);

        if (result.IsFailure)
        {
            return result.Error.ToProblem(httpContext);
        }

        return TypedResults.NoContent();
    }
}
