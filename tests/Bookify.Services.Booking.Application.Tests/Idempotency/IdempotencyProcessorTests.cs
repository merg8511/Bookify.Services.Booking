using Bookify.Services.Booking.Application.Abstractions.Idempotency;
using Bookify.Services.Booking.Application.Abstractions.Time;
using Bookify.Services.Booking.Application.Idempotency;
using Bookify.Services.Booking.Domain.Shared;

namespace Bookify.Services.Booking.Application.Tests.Idempotency;

public sealed class IdempotencyProcessorTests
{
    private static readonly DateTimeOffset UtcNow =
        new(
            2026,
            8,
            10,
            17,
            0,
            0,
            TimeSpan.Zero);

    [Fact]
    public async Task BeginAsync_WithNewKey_ReturnsExecute()
    {
        // ARRANGE
        var store = new FakeIdempotencyStore();

        var processor = CreateProcessor(store);

        IdempotencyRequestContext context = CreateContext();

        // ACT
        Result<IdempotencyProcessingResult> result = await processor.BeginAsync(context);

        // ASSERT
        Assert.True(result.IsSuccess);

        Assert.Equal(
            IdempotencyProcessingAction.Execute,
            result.Value.Action);

        Assert.Equal(
            1,
            store.ClaimCallCount);
    }

    [Fact]
    public async Task BeginAsync_WithCompletedRequest_ReturnsReplay()
    {
        // ARRANGE
        var store =
            new FakeIdempotencyStore
            {
                Stored =
                    new StoredIdempotencyRequest(
                        "HASH-A",
                        IdempotencyRequestStatus
                            .Completed,
                        StatusCode: 201,
                        ResponseBody:
                            """{"id":"123"}""",
                        ExpiresAt:
                            UtcNow.AddHours(1))
            };

        var processor = CreateProcessor(store);

        // ACT
        Result<IdempotencyProcessingResult> result = await processor.BeginAsync(CreateContext());

        // ASSERT
        Assert.True(result.IsSuccess);

        Assert.Equal(
            IdempotencyProcessingAction.Replay,
            result.Value.Action);

        Assert.Equal(
            201,
            result.Value.StatusCode);

        Assert.Equal(
            """{"id":"123"}""",
            result.Value.ResponseBody);
    }

    [Fact]
    public async Task BeginAsync_WithRequestInProgress_ReturnsConflict()
    {
        // ARRANGE
        var store =
            new FakeIdempotencyStore
            {
                Stored =
                    new StoredIdempotencyRequest(
                        "HASH-A",
                        IdempotencyRequestStatus
                            .InProgress,
                        StatusCode: null,
                        ResponseBody: null,
                        ExpiresAt:
                            UtcNow.AddHours(1))
            };

        var processor = CreateProcessor(store);

        // ACT
        Result<IdempotencyProcessingResult> result = await processor.BeginAsync(CreateContext());

        // ASSERT
        Assert.True(result.IsFailure);

        Assert.Equal(
            IdempotencyErrors.RequestInProgress,
            result.Error);
    }

    [Fact]
    public async Task BeginAsync_WithDifferentPayload_ReturnsConflict()
    {
        // ARRANGE
        var store =
            new FakeIdempotencyStore
            {
                Stored =
                    new StoredIdempotencyRequest(
                        "DIFFERENT-HASH",
                        IdempotencyRequestStatus
                            .Completed,
                        StatusCode: 201,
                        ResponseBody: "{}",
                        ExpiresAt:
                            UtcNow.AddHours(1))
            };

        var processor = CreateProcessor(store);

        // ACT
        Result<IdempotencyProcessingResult> result =
            await processor.BeginAsync(
                CreateContext());

        // ASSERT
        Assert.Equal(
            IdempotencyErrors
                .KeyPayloadMismatch,
            result.Error);
    }

    [Fact]
    public async Task BeginAsync_WithExpiredCompletedRequest_RestartsAndReturnsExecute()
    {
        // ARRANGE
        var store =
            new FakeIdempotencyStore
            {
                Stored =
                    new StoredIdempotencyRequest(
                        "OLD-HASH",
                        IdempotencyRequestStatus
                            .Completed,
                        StatusCode: 201,
                        ResponseBody: "{}",
                        ExpiresAt:
                            UtcNow.AddMinutes(-1))
            };

        var processor = CreateProcessor(store);

        IdempotencyRequestContext context = CreateContext();

        // ACT
        Result<IdempotencyProcessingResult> result = await processor.BeginAsync(context);

        // ASSERT
        Assert.True(result.IsSuccess);

        Assert.Equal(
            IdempotencyProcessingAction.Execute,
            result.Value.Action);

        Assert.Equal(
            1,
            store.ClaimCallCount);

        Assert.Equal(
            context.RequestHash,
            store.Stored?.RequestHash);

        Assert.Equal(
            IdempotencyRequestStatus.InProgress,
            store.Stored?.Status);
    }

    [Fact]
    public async Task BeginAsync_WithExpiredRequestStillInProgress_DoesNotRestart()
    {
        // ARRANGE
        var store =
            new FakeIdempotencyStore
            {
                Stored =
                    new StoredIdempotencyRequest(
                        "HASH-A",
                        IdempotencyRequestStatus
                            .InProgress,
                        StatusCode: null,
                        ResponseBody: null,
                        ExpiresAt:
                            UtcNow.AddMinutes(-1))
            };

        var processor = CreateProcessor(store);

        // ACT
        Result<IdempotencyProcessingResult> result = await processor.BeginAsync(CreateContext());

        // ASSERT
        Assert.Equal(
            IdempotencyErrors.RequestInProgress,
            result.Error);

        Assert.Equal(
            0,
            store.ClaimCallCount);
    }

    [Fact]
    public async Task CompleteAsync_StoresOriginalResponse()
    {
        // ARRANGE
        var store =
            new FakeIdempotencyStore
            {
                Stored =
                    new StoredIdempotencyRequest(
                        "HASH-A",
                        IdempotencyRequestStatus
                            .InProgress,
                        StatusCode: null,
                        ResponseBody: null,
                        ExpiresAt:
                            UtcNow.AddHours(1))
            };

        var processor = CreateProcessor(store);

        // ACT
        await processor.CompleteAsync(
            CreateContext(),
            statusCode: 201,
            responseBody:
                """{"id":"123"}""");

        // ASSERT
        Assert.Equal(
            1,
            store.CompleteCallCount);

        Assert.Equal(
            IdempotencyRequestStatus.Completed,
            store.Stored?.Status);

        Assert.Equal(
            201,
            store.Stored?.StatusCode);

        Assert.Equal(
            """{"id":"123"}""",
            store.Stored?.ResponseBody);
    }

    [Fact]
    public async Task BeginAsync_WhenConcurrentRequestClaimsKeyFirst_ReturnsInProgressConflict()
    {
        // Arrange
        var store = new ConcurrentWinnerStore();

        var processor =
            new IdempotencyProcessor(
                store,
                new FixedClock(
                    UtcNow));

        IdempotencyRequestContext context = CreateContext();

        // Act
        Result<IdempotencyProcessingResult> result = await processor.BeginAsync(context);

        // Assert
        Assert.True(result.IsFailure);

        Assert.Equal(
            IdempotencyErrors.RequestInProgress,
            result.Error);

        Assert.Equal(
            1,
            store.ClaimCallCount);
    }

    private sealed class ConcurrentWinnerStore :
        IIdempotencyStore
    {
        private int _getCallCount;
        public int ClaimCallCount { get; private set; }

        public Task<StoredIdempotencyRequest?>
            GetAsync(
                IdempotencyRequestContext context,
                CancellationToken cancellationToken = default)
        {
            _getCallCount++;

            if (_getCallCount == 1)
            {
                return Task.FromResult<StoredIdempotencyRequest?>(null);
            }

            return Task.FromResult<
                StoredIdempotencyRequest?>(
                    new StoredIdempotencyRequest(
                        context.RequestHash,
                        IdempotencyRequestStatus
                            .InProgress,
                        StatusCode: null,
                        ResponseBody: null,
                        ExpiresAt:
                            UtcNow.AddHours(24)));
        }

        public Task<bool> TryClaimAsync(
            IdempotencyRequestContext context,
            DateTimeOffset createdAt,
            DateTimeOffset expiresAt,
            CancellationToken cancellationToken = default)
        {
            ClaimCallCount++;

            // Otro request ganó la carrera.
            return Task.FromResult(false);
        }

        public Task CompleteAsync(
            IdempotencyRequestContext context,
            int statusCode,
            string? responseBody,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }

    private static IdempotencyProcessor CreateProcessor(FakeIdempotencyStore store)
    {
        return new IdempotencyProcessor(
            store,
            new FixedClock(
                UtcNow));
    }

    private static IdempotencyRequestContext CreateContext()
    {
        return new IdempotencyRequestContext(
            "KEY-A",
            "POST",
            "/api/v1/bookings",
            "HASH-A");
    }

    private sealed class FixedClock :
        IClock
    {
        public FixedClock(
            DateTimeOffset utcNow)
        {
            UtcNow =
                utcNow;
        }

        public DateTimeOffset UtcNow
        {
            get;
        }
    }

    private sealed class FakeIdempotencyStore :
        IIdempotencyStore
    {
        public StoredIdempotencyRequest? Stored { get; set; }
        public bool ClaimSucceeds { get; set; } = true;
        public int ClaimCallCount { get; private set; }
        public int CompleteCallCount { get; private set; }

        public Task<StoredIdempotencyRequest?> GetAsync(
            IdempotencyRequestContext context,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Stored);
        }

        public Task<bool> TryClaimAsync(
            IdempotencyRequestContext context,
            DateTimeOffset createdAt,
            DateTimeOffset expiresAt,
            CancellationToken cancellationToken = default)
        {
            ClaimCallCount++;

            if (!ClaimSucceeds)
            {
                return Task.FromResult(false);
            }

            Stored =
                new StoredIdempotencyRequest(
                    context.RequestHash,
                    IdempotencyRequestStatus.InProgress,
                    StatusCode: null,
                    ResponseBody: null,
                    expiresAt);

            return Task.FromResult(true);
        }

        public Task CompleteAsync(
            IdempotencyRequestContext context,
            int statusCode,
            string? responseBody,
            CancellationToken cancellationToken = default)
        {
            CompleteCallCount++;

            Stored =
                new StoredIdempotencyRequest(
                    context.RequestHash,
                    IdempotencyRequestStatus
                        .Completed,
                    statusCode,
                    responseBody,
                    Stored?.ExpiresAt ??
                        UtcNow.AddHours(24));

            return Task.CompletedTask;
        }
    }
}
