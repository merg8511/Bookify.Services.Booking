using Bookify.Services.Booking.Domain.Shared;

namespace Bookify.Services.Booking.Application.Abstractions.Payments;

public interface IStripeWebhookSignatureVerifier
{
    Result Verify(
        string rawBody,
        string signatureHeader,
        DateTimeOffset utcNow);
}
