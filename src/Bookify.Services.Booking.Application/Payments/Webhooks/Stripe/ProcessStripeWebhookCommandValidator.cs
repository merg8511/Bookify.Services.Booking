using Bookify.Services.Booking.Application.Abstractions.Messaging;
using Bookify.Services.Booking.Domain.Shared;

namespace Bookify.Services.Booking.Application.Payments.Webhooks.Stripe;

public sealed class ProcessStripeWebhookCommandValidator :
    IRequestValidator<ProcessStripeWebhookCommand>
{
    public Result Validate(ProcessStripeWebhookCommand request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.RawBody))
        {
            return Result.Failure(StripeWebhookErrors.InvalidPayload);
        }

        return Result.Success();
    }
}
