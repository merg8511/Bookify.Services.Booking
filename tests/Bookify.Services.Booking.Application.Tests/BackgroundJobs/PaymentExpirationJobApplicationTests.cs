
using Bookify.Services.Booking.Application.Abstractions.BackgroundJobs;
using Bookify.Services.Booking.Application.Abstractions.Messaging;
using Bookify.Services.Booking.Application.BackgroundJobs;
using Bookify.Services.Booking.Application.BackgroundJobs.Expiration;
using Bookify.Services.Booking.Application.Bookings.ExpirePayment;
using Bookify.Services.Booking.Application.Tests.Infrastructure;
using Bookify.Services.Booking.Domain.Shared;
using Microsoft.Extensions.Logging.Abstractions;

namespace Bookify.Services.Booking.Application.Tests.BackgroundJobs;

public sealed class PaymentExpirationJobApplicationTests
{
    private static readonly DateTimeOffset NowUtc =
        new(2026, 10, 28, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ExecuteBatchAsync_WhenClaimUnavailableOrCommandFails_ShouldContinue()
    {
        // ARRANGE
        Guid unavailableId = Guid.NewGuid();
        Guid failedId = Guid.NewGuid();
        Guid successfulId = Guid.NewGuid();

        var selector = new StubWorkSelector(
            [unavailableId, failedId, successfulId]);

        var claims = new SpyClaimManager();
        claims.DeniedIds.Add(unavailableId);

        var executor = new SpyCommandExecutor();

        executor.Results[failedId] = Result.Failure(
            Error.Failure(
                "Tests.ExpirationFailed",
                "Simulated functional failure."));

        PaymentExpirationJob job = CreateJob(
            selector,
            executor,
            claims);

        CancellationToken cancellationToken =
            TestContext.Current.CancellationToken;

        // ACT
        int processed = await job.ExecuteBatchAsync(
            3,
            cancellationToken);

        // ASSERT
        Assert.Equal(1, processed);

        Assert.Equal(NowUtc, selector.RequestedNowUtc);
        Assert.Equal(3, selector.RequestedBatchSize);

        Assert.Equal(
            [failedId, successfulId],
            executor.ExecutedBookingIds);

        Assert.Equal(3, claims.RequestedIds.Count);

        Assert.False(claims.CreatedClaims.ContainsKey(unavailableId));

        Assert.True(claims.CreatedClaims[failedId].IsDisposed);
        Assert.True(claims.CreatedClaims[successfulId].IsDisposed);
    }

    [Fact]
    public async Task ExecuteBatchAsync_WhenCommandThrows_ShouldContinueNextItem()
    {
        // ARRANGE
        Guid failingId = Guid.NewGuid();
        Guid successfulId = Guid.NewGuid();

        var selector = new StubWorkSelector(
            [failingId, successfulId]);

        var claims = new SpyClaimManager();
        var executor = new SpyCommandExecutor();

        executor.ExceptionIds.Add(failingId);

        PaymentExpirationJob job = CreateJob(
            selector,
            executor,
            claims);

        // ACT
        int processed = await job.ExecuteBatchAsync(
            2,
            TestContext.Current.CancellationToken);

        // ASSERT
        Assert.Equal(1, processed);

        Assert.Equal(
            [failingId, successfulId],
            executor.ExecutedBookingIds);

        Assert.True(claims.CreatedClaims[failingId].IsDisposed);
        Assert.True(claims.CreatedClaims[successfulId].IsDisposed);
    }

    [Fact]
    public async Task ExecuteBatchAsync_WhenCancelled_ShouldPropagateCancellation()
    {
        // ARRANGE
        var selector = new StubWorkSelector([Guid.NewGuid()]);
        var claims = new SpyClaimManager();
        var executor = new SpyCommandExecutor();

        PaymentExpirationJob job = CreateJob(
            selector,
            executor,
            claims);

        using var source = new CancellationTokenSource();
        await source.CancelAsync();

        // ACT
        Task Action() => job.ExecuteBatchAsync(
            1,
            source.Token);

        // ASSERT
        await Assert.ThrowsAnyAsync<OperationCanceledException>(Action);

        Assert.Empty(claims.RequestedIds);
        Assert.Empty(executor.ExecutedBookingIds);
    }

    [Fact]
    public async Task ExecuteBatchAsync_WithInvalidBatchSize_ShouldThrow()
    {
        // ARRANGE
        var selector = new StubWorkSelector([]);
        var claims = new SpyClaimManager();
        var executor = new SpyCommandExecutor();

        PaymentExpirationJob job = CreateJob(
            selector,
            executor,
            claims);

        // ACT
        Task Action() => job.ExecuteBatchAsync(0);

        // ASSERT
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(Action);

        Assert.Empty(claims.RequestedIds);
        Assert.Empty(executor.ExecutedBookingIds);
    }

    private static PaymentExpirationJob CreateJob(
        IPendingBookingWorkSelector selector,
        ICommandExecutor<ExpireBookingPaymentCommand> executor,
        IBackgroundJobClaimManager claims)
    {
        return new PaymentExpirationJob(
            selector,
            executor,
            claims,
            new FixedClock(NowUtc),
            NullLogger<PaymentExpirationJob>.Instance);
    }

    private sealed class StubWorkSelector : IPendingBookingWorkSelector
    {
        private readonly IReadOnlyList<Guid> _bookingIds;

        public StubWorkSelector(IReadOnlyList<Guid> bookingIds)
        {
            _bookingIds = bookingIds;
        }

        public DateTimeOffset? RequestedNowUtc { get; private set; }
        public int? RequestedBatchSize { get; private set; }

        public Task<IReadOnlyList<Guid>> GetExpiredPaymentBookingIdsAsync(
            DateTimeOffset nowUtc,
            int batchSize,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            RequestedNowUtc = nowUtc;
            RequestedBatchSize = batchSize;

            return Task.FromResult(_bookingIds);
        }

        public Task<IReadOnlyList<Guid>> GetExpiredApprovalBookingIdsAsync(
            DateTimeOffset nowUtc,
            int batchSize,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<IReadOnlyList<Guid>> GetDueCompletionBookingIdsAsync(
            DateTimeOffset nowUtc,
            int batchSize,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<IReadOnlyList<Guid>> GetPaymentDeadlineReminderCandidateIdsAsync(
            DateTimeOffset windowStartUtc,
            DateTimeOffset windowEndUtc,
            int batchSize,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<IReadOnlyList<Guid>> GetUpcomingCheckInReminderCandidateIdsAsync(
            DateTimeOffset windowStartUtc,
            DateTimeOffset windowEndUtc,
            int batchSize,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class SpyClaimManager : IBackgroundJobClaimManager
    {
        public HashSet<Guid> DeniedIds { get; } = [];

        public List<Guid> RequestedIds { get; } = [];

        public Dictionary<Guid, SpyClaim> CreatedClaims { get; } = [];

        public Task<IBackgroundJobClaim?> TryAcquireAsync(
            string jobKey,
            Guid workItemId,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            Assert.Equal(
                BackgroundJobClaimKeys.PaymentExpiration,
                jobKey);

            RequestedIds.Add(workItemId);

            if (DeniedIds.Contains(workItemId))
            {
                return Task.FromResult<IBackgroundJobClaim?>(null);
            }

            var claim = new SpyClaim();

            CreatedClaims.Add(workItemId, claim);

            return Task.FromResult<IBackgroundJobClaim?>(claim);
        }
    }

    private sealed class SpyClaim : IBackgroundJobClaim
    {
        public bool IsDisposed { get; private set; }

        public ValueTask DisposeAsync()
        {
            IsDisposed = true;
            return ValueTask.CompletedTask;
        }
    }

    private sealed class SpyCommandExecutor :
        ICommandExecutor<ExpireBookingPaymentCommand>
    {
        public Dictionary<Guid, Result> Results { get; } = [];

        public HashSet<Guid> ExceptionIds { get; } = [];

        public List<Guid> ExecutedBookingIds { get; } = [];

        public Task<Result> ExecuteAsync(
            ExpireBookingPaymentCommand command,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            ExecutedBookingIds.Add(command.BookingId);

            if (ExceptionIds.Contains(command.BookingId))
            {
                throw new InvalidOperationException(
                    "Simulated command failure.");
            }

            Result result = Results.TryGetValue(command.BookingId, out Result? configuredResult)
                ? configuredResult
                : Result.Success();

            return Task.FromResult(result);
        }
    }
}
