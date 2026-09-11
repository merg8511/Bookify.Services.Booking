using Bookify.Services.Booking.Application.Abstractions.Messaging;
using Bookify.Services.Booking.Application.Abstractions.Payments;
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

        if (string.IsNullOrWhiteSpace(request.SignatureHeader))
        {
            return Result.Failure(StripeWebhookSignatureErrors.SignatureRequired);
        }

        return Result.Success();
    }
}
