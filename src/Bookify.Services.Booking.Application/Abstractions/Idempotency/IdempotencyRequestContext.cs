namespace Bookify.Services.Booking.Application.Abstractions.Idempotency;

public sealed record IdempotencyRequestContext(
    string Key,
    string HttpMethod,
    string Endpoint,
    string RequestHash);
