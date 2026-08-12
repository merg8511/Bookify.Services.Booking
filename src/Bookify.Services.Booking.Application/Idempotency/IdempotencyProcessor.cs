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
            return await TryClaimOrResolveAsync(
                context,
                utcNow,
                cancellationToken);
        }

        if (IsExpiredCompleted(existingRequest, utcNow))
        {
            return await
                TryClaimOrResolveAsync(
                    context,
                    utcNow,
                    cancellationToken);
        }

        return ResolveExisting(
            existingRequest,
            context,
            utcNow);
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

    private async Task<
        Result<IdempotencyProcessingResult>>
        TryClaimOrResolveAsync(
        IdempotencyRequestContext context,
        DateTimeOffset utcNow,
        CancellationToken cancellationToken)
    {
        bool claimed =
            await _store.TryClaimAsync(
                context,
                utcNow,
                utcNow.Add(RetentionPeriod),
                cancellationToken);

        if (claimed)
        {
            return Result<
                IdempotencyProcessingResult>
                .Success(IdempotencyProcessingResult.Execute());
        }

        StoredIdempotencyRequest? winner =
            await _store.GetAsync(
                context,
                cancellationToken);

        if (winner is null)
        {
            throw new InvalidOperationException(
                "The idempotency key could not be " +
                "claimed, but the winning request " +
                "could not be found.");
        }

        return ResolveExisting(
            winner,
            context,
            utcNow);
    }

    private static Result<
        IdempotencyProcessingResult>
        ResolveExisting(
            StoredIdempotencyRequest request,
            IdempotencyRequestContext context,
            DateTimeOffset utcNow)
    {
        if (IsExpiredCompleted(request, utcNow))
        {
            throw new InvalidOperationException(
                "An expired completed idempotency " +
                "request remained unclaimed after " +
                "the concurrency resolution.");
        }

        if (!string.Equals(
            request.RequestHash,
            context.RequestHash,
            StringComparison.Ordinal))
        {
            return Result<
                IdempotencyProcessingResult>
                .Failure(IdempotencyErrors.KeyPayloadMismatch);
        }

        if (request.Status == IdempotencyRequestStatus.InProgress)
        {
            return Result<IdempotencyProcessingResult>
                .Failure(IdempotencyErrors.RequestInProgress);
        }

        if (request.Status == IdempotencyRequestStatus.Completed)
        {
            int statusCode =
                request.StatusCode ??
                throw new InvalidOperationException(
                    "A completed idempotency request " +
                    "must contain an HTTP status code.");

            return Result<IdempotencyProcessingResult>
                .Success(IdempotencyProcessingResult
                .Replay(statusCode, request.ResponseBody));
        }

        throw new InvalidOperationException(
            $"Unsopported idempotency status " +
            $"'{request.Status}'");
    }

    private static bool IsExpiredCompleted(
        StoredIdempotencyRequest request,
        DateTimeOffset utcNow)
    {
        return request.Status == IdempotencyRequestStatus.Completed &&
            request.ExpiresAt <= utcNow;
    }

    private static void ValidateContext(IdempotencyRequestContext context)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(context.Key);
        ArgumentException.ThrowIfNullOrWhiteSpace(context.HttpMethod);
        ArgumentException.ThrowIfNullOrWhiteSpace(context.Endpoint);
        ArgumentException.ThrowIfNullOrWhiteSpace(context.RequestHash);
    }
}
