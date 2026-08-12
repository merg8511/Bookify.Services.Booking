namespace Bookify.Services.Booking.Application.Idempotency;

public enum IdempotencyProcessingAction
{
    Execute = 1,
    Replay = 2
}
