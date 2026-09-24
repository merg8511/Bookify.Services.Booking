using Bookify.Services.Booking.Api.Extensions;
using Bookify.Services.Booking.Application.Abstractions.Messaging;
using Bookify.Services.Booking.Application.Bookings.Get;
using Bookify.Services.Booking.Domain.Shared;
using Microsoft.AspNetCore.Http.HttpResults;

using ApplicationBooking = Bookify.Services.Booking.Application.BookingDetailsReadModel;

namespace Bookify.Services.Booking.Api.Endpoints.Bookings.Get;

internal static class GetBookingEndpoint
{
    public static void Map(RouteGroupBuilder bookingsGroup)
    {
        bookingsGroup
            .MapGet("/{identifier}", HandleAsync)
            .WithName(EndpointNames.Bookings.GetByIdentifier)
            .WithSummary("Gets a booking by ID or public booking reference.")
            .Produces<GetBookingResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status500InternalServerError);
    }

    private static async Task<Results<Ok<GetBookingResponse>, ProblemHttpResult>> HandleAsync(
        string identifier,
        IQueryExecutor<GetBookingQuery, ApplicationBooking> executor,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var query = new GetBookingQuery(identifier);

        Result<ApplicationBooking> result = await executor.ExecuteAsync(query, cancellationToken);

        return result.ToHttpResult(
            httpContext,
            booking => TypedResults.Ok(MapToResponse(booking)));
    }

    private static GetBookingResponse MapToResponse(ApplicationBooking booking)
    {
        GetBookingGuestResponse? guest =
            booking.GuestFullName is not null &&
            booking.GuestEmail is not null &&
            booking.GuestPhone is not null
                ? new GetBookingGuestResponse(
                    booking.GuestFullName,
                    booking.GuestEmail,
                    booking.GuestPhone)
                : null;

        GetBookingPriceResponse? price =
            booking.AccommodationPrice.HasValue &&
            booking.ExtraGuestPrice.HasValue &&
            booking.TotalPrice.HasValue &&
            !string.IsNullOrWhiteSpace(booking.Currency)
                ? new GetBookingPriceResponse(
                    booking.AccommodationPrice.Value,
                    booking.ExtraGuestPrice.Value,
                    booking.TotalPrice.Value,
                    booking.Currency)
                : null;

        var rentableUnit = new GetBookingRentableUnitResponse(
            booking.RentableUnitId,
            booking.RentableUnitName);

        return new GetBookingResponse(
            booking.Id,
            booking.BookingReference,
            booking.PropertyId,
            booking.PropertyName,
            rentableUnit,
            booking.CheckInDate,
            booking.CheckOutDate,
            booking.NumberOfNights,
            booking.GuestCount,
            guest,
            price,
            booking.Status,
            booking.CancellationReason,
            booking.PaymentStatus,
            booking.CreatedAtUtc,
            booking.ApprovalDueAtUtc,
            booking.ApprovedAtUtc,
            booking.PaymentDueAtUtc,
            booking.PaidAtUtc,
            booking.CancelledAtUtc,
            booking.CompletedAtUtc);
    }
}
