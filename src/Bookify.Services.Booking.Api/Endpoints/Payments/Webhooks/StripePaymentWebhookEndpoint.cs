using Bookify.Services.Booking.Api.Extensions;
using Bookify.Services.Booking.Application.Abstractions.Messaging;
using Bookify.Services.Booking.Application.Payments.Webhooks.Stripe;
using Bookify.Services.Booking.Domain.Shared;
using Microsoft.AspNetCore.Http.HttpResults;
using System.Text;

namespace Bookify.Services.Booking.Api.Endpoints.Payments.Webhooks;

internal static class StripePaymentWebhookEndpoint
{
    public static void Map(RouteGroupBuilder paymentsGroup)
    {
        paymentsGroup
            .MapPost("webhooks/stripe", HandleAsync)
            .WithName(EndpointNames.Payments.StripeWebhook)
            .WithSummary("Receives Stripe payment webhook events.")
            .Produces(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status500InternalServerError);
    }

    private static async Task<Results<Ok, ProblemHttpResult>> HandleAsync(
        HttpContext httpContext,
        ICommandExecutor<ProcessStripeWebhookCommand> commandExecutor,
        CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(
            httpContext.Request.Body,
            Encoding.UTF8,
            detectEncodingFromByteOrderMarks: false,
            bufferSize: 1024,
            leaveOpen: true);

        string rawBody = await reader.ReadToEndAsync(cancellationToken);
        var command = new ProcessStripeWebhookCommand(rawBody);
        Result result = await commandExecutor.ExecuteAsync(command, cancellationToken);

        if (result.IsFailure)
        {
            return result.Error.ToProblem(httpContext);
        }

        return TypedResults.Ok();
    }
}
