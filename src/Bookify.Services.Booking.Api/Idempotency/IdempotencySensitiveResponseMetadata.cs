namespace Bookify.Services.Booking.Api.Idempotency;

internal sealed class IdempotencySensitiveResponseMetadata
{
    public static readonly IdempotencySensitiveResponseMetadata Instance = new();

    private IdempotencySensitiveResponseMetadata()
    {
    }
}
