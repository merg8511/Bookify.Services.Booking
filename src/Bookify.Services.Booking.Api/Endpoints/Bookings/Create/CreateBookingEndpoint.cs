using Bookify.Services.Booking.Api.Extensions;
using Bookify.Services.Booking.Api.Idempotency;
using Bookify.Services.Booking.Application.Abstractions.Messaging;
using Bookify.Services.Booking.Application.Bookings.Create;
using Bookify.Services.Booking.Domain.Bookings;
using Bookify.Services.Booking.Domain.Shared;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Bookify.Services.Booking.Api.Endpoints.Bookings.Create;

internal static class CreateBookingEndpoint
{
    public static void Map(RouteGroupBuilder bookingsGroup)
    {
        bookingsGroup
            .MapPost(
                "/",
                HandleAsync)
            .WithName(
                EndpointNames.Bookings.Create)
            .WithSummary("Creates a new booking.")
            .WithMetadata(IdempotencyRequiredMetadata.Instance)
            .Accepts<CreateBookingRequest>("application/json")
            .Produces<CreateBookingResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .Produces(StatusCodes.Status415UnsupportedMediaType)
            .ProducesProblem(StatusCodes.Status500InternalServerError);
    }

    private static async Task<
        Results<
            Created<CreateBookingResponse>,
            ProblemHttpResult>> HandleAsync(
        CreateBookingRequest request,
        ICommandExecutor<
            CreateBookingCommand,
            CreateBookingResult> commandExecutor,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var command =
            new CreateBookingCommand(
                request.PropertyId,
                request.RentableUnitId,
                request.CheckInDate,
                request.CheckOutDate,
                request.GuestCount);

        Result<CreateBookingResult> result = await commandExecutor.ExecuteAsync(command, cancellationToken);

        return result.ToHttpResult(
            httpContext,
            booking =>
            {
                var price =
                    new CreateBookingPriceResponse(
                        booking.AccommodationPrice,
                        booking.ExtraGuestPrice,
                        booking.TotalPrice,
                        booking.Currency);

                var response =
                    new CreateBookingResponse(
                        booking.Id,
                        booking.Status.ToString(),
                        price);

                string location = BookingsEndpoints
                                    .GetResourceLocation(booking.Id);

                return TypedResults.Created(
                    location,
                    response);
            });
    }
}
