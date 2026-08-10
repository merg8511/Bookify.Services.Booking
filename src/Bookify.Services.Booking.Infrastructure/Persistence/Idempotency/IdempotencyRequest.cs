namespace Bookify.Services.Booking.Infrastructure.Persistence.Idempotency;

internal sealed class IdempotencyRequest
{
    public IdempotencyRequest()
    {
    }

    public IdempotencyRequest(
        Guid id,
        string key,
        string httpMethod,
        string endpoint,
        string requestHash,
        DateTimeOffset createdAt,
        DateTimeOffset expiresAt)
    {
        Id = id;
        Key = key;
        HttpMethod = httpMethod;
        Endpoint = endpoint;
        RequestHash = requestHash;
        Status = IdempotencyRequestStatus.InProgress;
        CreatedAt = createdAt;
        ExpiresAt = expiresAt;
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

    public static IdempotencyRequest Create(
        string key,
        string httpMethod,
        string endpoint,
        string requestHash,
        DateTimeOffset createdAt,
        DateTimeOffset expiresAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentException.ThrowIfNullOrWhiteSpace(httpMethod);
        ArgumentException.ThrowIfNullOrWhiteSpace(endpoint);
        ArgumentException.ThrowIfNullOrWhiteSpace(requestHash);

        if (expiresAt <= createdAt)
        {
            throw new ArgumentOutOfRangeException(
                nameof(expiresAt),
                "The expiration time must be later " +
                "than the creation time.");
        }

        return new IdempotencyRequest(
            Guid.NewGuid(),
            key,
            httpMethod,
            endpoint,
            requestHash,
            createdAt,
            expiresAt);
    }

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
