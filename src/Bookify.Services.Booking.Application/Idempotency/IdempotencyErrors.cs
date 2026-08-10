using Bookify.Services.Booking.Domain.Shared;

namespace Bookify.Services.Booking.Application.Idempotency;

public static class IdempotencyErrors
{
    public static readonly Error RequestInProgress =
        Error.Conflict(
            "Idempotency.RequestInProgress",
            "A request with the same idempotency key " +
            "is already being processed.");

    public static readonly Error KeyPayloadMismatch =
        Error.Conflict(
            "Idempotency.KeyPayloadMismatch",
            "The idempotency key has already been used " +
            "with different request.");
}
