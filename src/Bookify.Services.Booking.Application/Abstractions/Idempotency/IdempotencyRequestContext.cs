namespace Bookify.Services.Booking.Application.Abstractions.Idempotency;

public sealed record IdempotencyRequestContext(
    string CallerScope,
    string Key,
    string HttpMethod,
    string Endpoint,
    string RequestHash);
