using Bookify.Services.Booking.Api.Extensions;
using Bookify.Services.Booking.Api.Security;
using Bookify.Services.Booking.Application.Abstractions.Messaging;
using Bookify.Services.Booking.Application.Payments.Administrative.GetByBooking;
using Bookify.Services.Booking.Application.Payments.Administrative.ReadModels;
using Bookify.Services.Booking.Domain.Shared;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Bookify.Services.Booking.Api.Endpoints.Administrative.Bookings.Payment;

internal static class AdminBookingPaymentEndpoint
{
    public static void Map(RouteGroupBuilder bookingsGroup)
    {
        ArgumentNullException.ThrowIfNull(bookingsGroup);

        bookingsGroup
            .MapGet("/{bookingId:guid}/payment", HandleAsync)
            .WithName(EndpointNames.AdminBookings.GetPayment)
            .WithSummary("Gets administrative payment details and attempts for a booking.")
            .RequirePropertyOwnerOrAdminAccessFromRoute("propertyId")
            .Produces<GetAdminBookingPaymentResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status500InternalServerError);
    }

    private static async Task<Results<Ok<GetAdminBookingPaymentResponse>, ProblemHttpResult>> HandleAsync(
        Guid propertyId,
        Guid bookingId,
        IQueryExecutor<GetAdminBookingPaymentQuery, AdminBookingPaymentReadModel> executor,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var query = new GetAdminBookingPaymentQuery(propertyId, bookingId);

        Result<AdminBookingPaymentReadModel> result = await executor.ExecuteAsync(query, cancellationToken);

        return result.ToHttpResult(
            httpContext,
            payment => TypedResults.Ok(MapToResponse(propertyId, payment)));
    }

    private static GetAdminBookingPaymentResponse MapToResponse(
        Guid propertyId,
        AdminBookingPaymentReadModel model)
    {
        AdminPaymentDetailsResponse? paymentResponse = null;

        if (model.Payment is not null)
        {
            AdminPaymentReadModel payment = model.Payment;

            AdminPaymentAttemptResponse[] attempts = payment.Attempts
                .Select(attempt =>
                    new AdminPaymentAttemptResponse(
                        attempt.Id,
                        attempt.Status,
                        attempt.ExternalReference,
                        attempt.Amount,
                        attempt.Currency,
                        attempt.CreatedAtUtc,
                        attempt.CompletedAtUtc))
                .ToArray();

            paymentResponse = new AdminPaymentDetailsResponse(
                payment.Id,
                payment.Status,
                payment.Amount,
                payment.Currency,
                payment.CreatedAtUtc,
                payment.UpdatedAtUtc,
                payment.CompletedAtUtc,
                payment.HasPendingInitiation,
                payment.PendingInitiationCreatedAtUtc,
                attempts);
        }

        return new GetAdminBookingPaymentResponse(
            propertyId,
            model.BookingId,
            model.BookingStatus,
            paymentResponse);
    }
}
