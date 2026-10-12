using Bookify.Services.Booking.Application.Abstractions.BackgroundJobs;
using Bookify.Services.Booking.Application.Abstractions.Messaging;
using Bookify.Services.Booking.Application.Abstractions.Time;
using Bookify.Services.Booking.Application.Bookings.ExpireApproval;
using Bookify.Services.Booking.Application.Observability;
using Bookify.Services.Booking.Domain.Shared;
using Microsoft.Extensions.Logging;

namespace Bookify.Services.Booking.Application.BackgroundJobs.Expiration;

public sealed class ApprovalExpirationJob : IBackgroundJob
{
    private readonly IPendingBookingWorkSelector _workSelector;
    private readonly ICommandExecutor<ExpireBookingApprovalCommand> _commandExecutor;
    private readonly IBackgroundJobClaimManager _claimManager;
    private readonly IClock _clock;
    private readonly ILogger<ApprovalExpirationJob> _logger;

    public ApprovalExpirationJob(
        IPendingBookingWorkSelector workSelector,
        ICommandExecutor<ExpireBookingApprovalCommand> commandExecutor,
        IBackgroundJobClaimManager claimManager,
        IClock clock,
        ILogger<ApprovalExpirationJob> logger)
    {
        _workSelector = workSelector ??
            throw new ArgumentNullException(nameof(workSelector));

        _commandExecutor = commandExecutor ??
            throw new ArgumentNullException(nameof(commandExecutor));

        _claimManager = claimManager ??
            throw new ArgumentNullException(nameof(claimManager));

        _clock = clock ??
            throw new ArgumentNullException(nameof(clock));

        _logger = logger ??
            throw new ArgumentNullException(nameof(logger));
    }
    public async Task<int> ExecuteBatchAsync(int batchSize, CancellationToken cancellationToken = default)
    {
        if (batchSize <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(batchSize),
                batchSize,
                "Batch size must be greater than zero.");
        }

        DateTimeOffset nowUtc = _clock.UtcNow;

        IReadOnlyList<Guid> bookingIds = await _workSelector.GetExpiredApprovalBookingIdsAsync(
            nowUtc,
            batchSize,
            cancellationToken);

        int processedCount = 0;

        foreach (Guid bookingId in bookingIds)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                IBackgroundJobClaim? claim = await _claimManager.TryAcquireAsync(
                    BackgroundJobClaimKeys.ApprovalExpiration,
                    bookingId,
                    cancellationToken);

                if (claim is null)
                {
                    continue;
                }

                await using (claim)
                {
                    Result result = await _commandExecutor.ExecuteAsync(
                    new ExpireBookingApprovalCommand(bookingId),
                    cancellationToken);

                    if (result.IsFailure)
                    {
                        _logger.LogWarning(
                            ApplicationLogEvents.ApprovalExpirationFailed,
                            "Could not expire approval for booking {BookingId}. ErrorCode: {ErrorCode}.",
                            bookingId,
                            result.Error.Code);

                        continue;
                    }

                    processedCount++;
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                _logger.LogError(
                    ApplicationLogEvents.ApprovalExpirationUnhandledException,
                    exception,
                    "Unexpected error expiring approval for booking {BookingId}.", bookingId);
            }
        }

        return processedCount;
    }
}
