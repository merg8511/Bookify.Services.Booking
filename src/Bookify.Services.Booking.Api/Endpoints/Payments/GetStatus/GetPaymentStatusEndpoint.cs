using Bookify.Services.Booking.Api.Extensions;
using Bookify.Services.Booking.Application.Abstractions.Messaging;
using Bookify.Services.Booking.Application.Payments.GetStatus;
using Bookify.Services.Booking.Domain.Shared;
using Microsoft.AspNetCore.Http.HttpResults;

using ApplicationPaymentStatus = Bookify.Services.Booking.Application.Payments.ReadModels.PaymentStatusReadModel;

namespace Bookify.Services.Booking.Api.Endpoints.Payments.GetStatus;

internal static class GetPaymentStatusEndpoint
{
    public static void Map(RouteGroupBuilder paymentsGroup)
    {
        paymentsGroup
            .MapGet("/bookings/{bookingId:guid}/status", HandleAsync)
            .WithName(EndpointNames.Payments.GetStatus)
            .WithSummary("Gets the authoritative payment status for a booking.")
            .Produces<GetPaymentStatusResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status500InternalServerError);
    }

    private static async Task<Results<
        Ok<GetPaymentStatusResponse>, ProblemHttpResult>> HandleAsync(
            Guid bookingId,
            IQueryExecutor<GetPaymentStatusQuery, ApplicationPaymentStatus> executor,
            HttpContext httpContext,
            CancellationToken cancellationToken)
    {
        var query = new GetPaymentStatusQuery(bookingId);

        Result<ApplicationPaymentStatus> result = await executor
            .ExecuteAsync(query, cancellationToken);

        return result.ToHttpResult(
            httpContext,
            paymentStatus => TypedResults.Ok(MapToResponse(paymentStatus)));
    }

    private static GetPaymentStatusResponse MapToResponse(
        ApplicationPaymentStatus paymentStatus)
    {
        return new GetPaymentStatusResponse(
            paymentStatus.BookingId,
            paymentStatus.BookingStatus,
            paymentStatus.PaymentStatus,
            paymentStatus.PaymentAttemptStatus);
    }
}
