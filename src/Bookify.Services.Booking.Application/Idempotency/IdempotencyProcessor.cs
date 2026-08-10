using Bookify.Services.Booking.Application.Abstractions.Idempotency;
using Bookify.Services.Booking.Application.Abstractions.Time;
using Bookify.Services.Booking.Domain.Shared;

namespace Bookify.Services.Booking.Application.Idempotency;

public sealed class IdempotencyProcessor : IIdempotencyProcessor
{
    private static readonly TimeSpan RetentionPeriod = TimeSpan.FromHours(24);
    private readonly IIdempotencyStore _store;
    private readonly IClock _clock;

    public IdempotencyProcessor(
        IIdempotencyStore store,
        IClock clock)
    {
        _store = store ??
            throw new ArgumentNullException(nameof(store));

        _clock = clock
            ?? throw new ArgumentNullException(nameof(clock));
    }

    public async Task<Result<IdempotencyProcessingResult>>
        BeginAsync(
        IdempotencyRequestContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ValidateContext(context);
        DateTimeOffset utcNow = _clock.UtcNow;

        StoredIdempotencyRequest? existingRequest =
            await _store.GetAsync(
                context,
                cancellationToken);

        if (existingRequest is null)
        {
            await _store.CreateAsync(
                context,
                utcNow,
                utcNow.Add(RetentionPeriod),
                cancellationToken);

            return Result<IdempotencyProcessingResult>
                .Success(IdempotencyProcessingResult.Execute());
        }

        if (existingRequest.Status == IdempotencyRequestStatus.Completed &&
            existingRequest.ExpiresAt <= utcNow)
        {
            await _store.RestartAsync(
                context,
                utcNow,
                utcNow.Add(RetentionPeriod),
                cancellationToken);

            return Result<IdempotencyProcessingResult>
                .Success(IdempotencyProcessingResult.Execute());
        }

        if (!string.Equals(
            existingRequest.RequestHash,
            context.RequestHash,
            StringComparison.Ordinal))
        {
            return Result<IdempotencyProcessingResult>
                .Failure(IdempotencyErrors.KeyPayloadMismatch);
        }

        if (existingRequest.Status == IdempotencyRequestStatus.InProgress)
        {
            return Result<IdempotencyProcessingResult>
                .Failure(IdempotencyErrors.RequestInProgress);
        }

        if (existingRequest.Status == IdempotencyRequestStatus.Completed)
        {
            int statusCode =
                existingRequest.StatusCode ??
                throw new InvalidOperationException(
                    "A completed idempotency request " +
                    "must contain an HTTP status code.");

            return Result<IdempotencyProcessingResult>
                .Success(
                IdempotencyProcessingResult
                    .Replay(
                        statusCode,
                        existingRequest
                            .ResponseBody));
        }

        throw new InvalidOperationException(
            $"Unsupported idempotency request status " +
            $"'{existingRequest.Status}'.");
    }

    public Task CompleteAsync(
        IdempotencyRequestContext context,
        int statusCode,
        string? responseBody,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ValidateContext(context);

        if (statusCode is < 100 or > 599)
        {
            throw new ArgumentOutOfRangeException(nameof(statusCode));
        }

        return _store.CompleteAsync(
                context,
                statusCode,
                responseBody,
                cancellationToken);
    }

    private static void ValidateContext(IdempotencyRequestContext context)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(context.Key);
        ArgumentException.ThrowIfNullOrWhiteSpace(context.HttpMethod);
        ArgumentException.ThrowIfNullOrWhiteSpace(context.Endpoint);
        ArgumentException.ThrowIfNullOrWhiteSpace(context.RequestHash);
    }
}
