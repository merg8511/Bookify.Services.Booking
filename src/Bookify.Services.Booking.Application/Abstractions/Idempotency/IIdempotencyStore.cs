namespace Bookify.Services.Booking.Application.Abstractions.Idempotency;

public interface IIdempotencyStore
{
    Task<StoredIdempotencyRequest?> GetAsync(
        IdempotencyRequestContext context,
        CancellationToken cancellationToken = default);

    Task CreateAsync(
        IdempotencyRequestContext context,
        DateTimeOffset createdAt,
        DateTimeOffset expiresAt,
        CancellationToken cancellationToken = default);

    Task RestartAsync(
        IdempotencyRequestContext context,
        DateTimeOffset createdAt,
        DateTimeOffset expiresAt,
        CancellationToken cancellationToken = default);

    Task CompleteAsync(
        IdempotencyRequestContext context,
        int statusCode,
        string? responseBoby,
        CancellationToken cancellationToken = default);
}
