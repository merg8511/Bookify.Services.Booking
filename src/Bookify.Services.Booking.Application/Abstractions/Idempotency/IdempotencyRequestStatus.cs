namespace Bookify.Services.Booking.Application.Abstractions.Idempotency;

public enum IdempotencyRequestStatus
{
    InProgress = 1,
    Completed = 2
}
