using Bookify.Services.Booking.Domain.Shared;

namespace Bookify.Services.Booking.Api.Idempotency;

internal static class IdempotencyHttpErrors
{
    internal static readonly Error KeyRequired =
        Error.Validation(
            "Idempotency.KeyRequired",
            "The idempotency-key header is required.");

    internal static readonly Error InvalidKey =
        Error.Validation(
            "Idempotency.InvalidKey",
            "The idempotency-key header must contain " +
            "a single non-empty value with a maximum " +
            "length of 255 characters.");
}
