using Bookify.Services.Booking.Application.Abstractions.Payments;
using Bookify.Services.Booking.Domain.Shared;

using StripeEventUtility = global::Stripe.EventUtility;
using StripeException = global::Stripe.StripeException;

namespace Bookify.Services.Booking.Infrastructure.Payments.Stripe;

internal sealed class StripeWebhookSignatureVerifier : IStripeWebhookSignatureVerifier
{
    internal const long DefaultToleranceSeconds = 300;
    private readonly string _webhookSecret;
    private readonly long _toleranceSeconds;

    public StripeWebhookSignatureVerifier(string webhookSecret, long toleranceSeconds)
    {
        if (toleranceSeconds <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(toleranceSeconds),
                "Stripe webhook tolerance must be greater than zero.");
        }

        _webhookSecret = webhookSecret.Trim() ?? string.Empty;
        _toleranceSeconds = toleranceSeconds;
    }

    public Result Verify(string rawBody, string signatureHeader, DateTimeOffset utcNow)
    {
        if (string.IsNullOrWhiteSpace(_webhookSecret))
        {
            return Result.Failure(StripeWebhookSignatureErrors.WebhookSecretNotConfigured);
        }

        if (string.IsNullOrWhiteSpace(rawBody) ||
            string.IsNullOrWhiteSpace(signatureHeader))
        {
            return
                 Result.Failure(StripeWebhookSignatureErrors.InvalidSignature);
        }

        try
        {
            StripeEventUtility.ValidateSignature(
                rawBody,
                signatureHeader,
                _webhookSecret,
                _toleranceSeconds,
                utcNow.ToUnixTimeSeconds());

            return Result.Success();
        }
        catch (StripeException)
        {
            return Result.Failure(StripeWebhookSignatureErrors.InvalidSignature);
        }
        catch (FormatException)
        {
            return Result.Failure(StripeWebhookSignatureErrors.InvalidSignature);
        }
        catch (OverflowException)
        {
            return Result.Failure(StripeWebhookSignatureErrors.InvalidSignature);
        }
    }
}
