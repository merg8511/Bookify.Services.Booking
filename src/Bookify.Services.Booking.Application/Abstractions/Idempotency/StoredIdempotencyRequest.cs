namespace Bookify.Services.Booking.Application.Abstractions.Idempotency;

public sealed record StoredIdempotencyRequest(
    string RequestHash,
    IdempotencyRequestStatus Status,
    int? StatusCode,
    string? ResponseBody,
    DateTimeOffset ExpiresAt);
