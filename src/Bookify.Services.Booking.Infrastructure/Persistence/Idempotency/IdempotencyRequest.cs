using Bookify.Services.Booking.Application.Abstractions.Idempotency;

namespace Bookify.Services.Booking.Infrastructure.Persistence.Idempotency;

internal sealed class IdempotencyRequest
{
    public IdempotencyRequest()
    {
    }

    public Guid Id { get; private set; }
    public string Key { get; private set; } = string.Empty;
    public string HttpMethod { get; private set; } = string.Empty;
    public string Endpoint { get; private set; } = string.Empty;
    public string RequestHash { get; private set; } = string.Empty;
    public IdempotencyRequestStatus Status { get; private set; }
    public int? StatusCode { get; private set; }
    public string? ResponseBody { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }

    public void Complete(
        int statusCode,
        string? responseBody)
    {
        if (Status == IdempotencyRequestStatus.Completed)
        {
            throw new InvalidOperationException(
                "The idempotency request has already " +
                "been completed.");
        }

        if (statusCode is < 100 or > 599)
        {
            throw new ArgumentOutOfRangeException(nameof(statusCode));
        }

        StatusCode = statusCode;
        ResponseBody = responseBody;
        Status = IdempotencyRequestStatus.Completed;
    }
}
