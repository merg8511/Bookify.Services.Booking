using Bookify.Services.Booking.Api.Extensions;
using Bookify.Services.Booking.Api.Idempotency;
using Bookify.Services.Booking.Application.Abstractions.Messaging;
using Bookify.Services.Booking.Application.Payments.Initiate;
using Bookify.Services.Booking.Domain.Shared;
using Microsoft.AspNetCore.Http.HttpResults;

using ApplicationInitiatePaymentResponse = Bookify.Services.Booking.Application.Payments.Initiate.InitiatePaymentResponse;

namespace Bookify.Services.Booking.Api.Endpoints.Payments.Initiate;

internal static class InitiatePaymentEndpoint
{
    private const string IdempotencyHeaderName = "Idempotency-Key";

    public static void Map(RouteGroupBuilder paymentsGroup)
    {
        paymentsGroup
            .MapPost(
                "/",
                HandleAsync)
            .WithName(EndpointNames.Payments.Initiate)
            .WithSummary("Initiates a payment for a booking.")
            .WithMetadata(IdempotencyRequiredMetadata.Instance)
            .WithMetadata(IdempotencySensitiveResponseMetadata.Instance)
            .Accepts<InitiatePaymentRequest>("application/json")
            .Produces<InitiatePaymentResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status415UnsupportedMediaType)
            .ProducesProblem(StatusCodes.Status500InternalServerError);
    }

    private static async Task<Results<
        Ok<InitiatePaymentResponse>,
        ProblemHttpResult>> HandleAsync(
            InitiatePaymentRequest request,
            ICommandExecutor<
                InitiatePaymentCommand,
                ApplicationInitiatePaymentResponse> commandExecutor,
            HttpContext httpContext,
            CancellationToken cancellationToken)
    {
        string idempotencyKey = httpContext.Request
            .Headers[IdempotencyHeaderName]
            .ToString();

        var command =
            new InitiatePaymentCommand(
                request.BookingId,
                idempotencyKey);

        Result<ApplicationInitiatePaymentResponse> result =
            await commandExecutor.ExecuteAsync(
                command,
                cancellationToken);

        return result.ToHttpResult(
            httpContext,
            payment =>
            {
                var response = new InitiatePaymentResponse(
                    payment.PaymentId,
                    payment.PaymentAttemptId,
                    payment.Status.ToString(),
                    payment.Amount,
                    payment.Currency,
                    payment.ClientSecret);

                return TypedResults.Ok(response);
            });
    }
}
