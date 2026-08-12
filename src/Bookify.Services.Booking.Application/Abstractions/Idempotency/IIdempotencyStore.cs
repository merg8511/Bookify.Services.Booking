namespace Bookify.Services.Booking.Application.Abstractions.Idempotency;

public interface IIdempotencyStore
{
    Task<StoredIdempotencyRequest?> GetAsync(
        IdempotencyRequestContext context,
        CancellationToken cancellationToken = default);

    Task<bool> TryClaimAsync(
        IdempotencyRequestContext context,
        DateTimeOffset createdAt,
        DateTimeOffset expiresAt,
        CancellationToken cancellationToken = default);

    Task CompleteAsync(
        IdempotencyRequestContext context,
        int statusCode,
        string? responseBody,
        CancellationToken cancellationToken = default);
}
