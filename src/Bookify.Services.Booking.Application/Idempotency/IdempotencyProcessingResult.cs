namespace Bookify.Services.Booking.Application.Idempotency;

public sealed record IdempotencyProcessingResult
{
    private IdempotencyProcessingResult(
        IdempotencyProcessingAction action,
        int? statusCode,
        string? responseBody)
    {
        Action = action;
        StatusCode = statusCode;
        ResponseBody = responseBody;
    }

    public IdempotencyProcessingAction Action { get; }
    public int? StatusCode { get; }
    public string? ResponseBody { get; }

    public static IdempotencyProcessingResult Execute()
    {
        return new IdempotencyProcessingResult(
            IdempotencyProcessingAction.Execute,
            statusCode: null,
            responseBody: null);
    }

    public static IdempotencyProcessingResult Replay(
        int statusCode,
        string? responseBody)
    {
        if (statusCode is < 100 or > 599)
        {
            throw new ArgumentOutOfRangeException(nameof(statusCode));
        }

        return new IdempotencyProcessingResult(
            IdempotencyProcessingAction.Replay,
            statusCode,
            responseBody);

    }
}
