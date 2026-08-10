namespace Bookify.Services.Booking.Infrastructure.Persistence.Idempotency;

internal enum IdempotencyRequestStatus
{
    InProgress = 1,
    Completed = 2
}
