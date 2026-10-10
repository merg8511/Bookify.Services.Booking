using Bookify.Services.Booking.Application.Abstractions.BackgroundJobs;
using Bookify.Services.Booking.Application.Abstractions.Messaging;
using Bookify.Services.Booking.Application.Abstractions.Time;
using Bookify.Services.Booking.Application.Bookings.ExpirePayment;
using Bookify.Services.Booking.Domain.Shared;
using Microsoft.Extensions.Logging;

namespace Bookify.Services.Booking.Application.BackgroundJobs.Expiration;

public sealed class PaymentExpirationJob : IBackgroundJob
{
    private readonly IPendingBookingWorkSelector _workSelector;
    private readonly ICommandExecutor<ExpireBookingPaymentCommand> _commandExecutor;
    private readonly IBackgroundJobClaimManager _claimManager;
    private readonly IClock _clock;
    private readonly ILogger<PaymentExpirationJob> _logger;

    public PaymentExpirationJob(
        IPendingBookingWorkSelector workSelector,
        ICommandExecutor<ExpireBookingPaymentCommand> commandExecutor,
        IBackgroundJobClaimManager claimManager,
        IClock clock,
        ILogger<PaymentExpirationJob> logger)
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

        IReadOnlyList<Guid> bookingIds = await _workSelector
            .GetExpiredPaymentBookingIdsAsync(nowUtc, batchSize, cancellationToken);

        int processedCount = 0;

        foreach (Guid bookingId in bookingIds)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                IBackgroundJobClaim? claim = await _claimManager.TryAcquireAsync(
                    BackgroundJobClaimKeys.PaymentExpiration,
                    bookingId,
                    cancellationToken);

                if (claim is null)
                {
                    continue;
                }

                await using (claim)
                {
                    Result result = await _commandExecutor.ExecuteAsync(
                    new ExpireBookingPaymentCommand(bookingId),
                    cancellationToken);

                    if (result.IsFailure)
                    {
                        _logger.LogWarning("Could not expire payment for booking " +
                            "{BookingId}. ErrorCode: {ErrorCode}.",
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
                    exception,
                    "Unexpected error expiring payment for booking {BookingId}.", bookingId);
            }
        }

        return processedCount;
    }
}
