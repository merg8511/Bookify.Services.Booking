namespace Bookify.Services.Booking.Api.Idempotency;

internal sealed class IdempotencyRequiredMetadata
{
    internal static readonly IdempotencyRequiredMetadata Instance = new();

    private IdempotencyRequiredMetadata()
    {
    }
}
