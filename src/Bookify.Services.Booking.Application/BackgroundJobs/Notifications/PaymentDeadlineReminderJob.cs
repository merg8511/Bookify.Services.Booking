using Bookify.Services.Booking.Application.Abstractions.BackgroundJobs;
using Bookify.Services.Booking.Application.Abstractions.Time;
using Bookify.Services.Booking.Application.Notifications;
using Bookify.Services.Booking.Domain.Shared;
using Microsoft.Extensions.Logging;

namespace Bookify.Services.Booking.Application.BackgroundJobs.Notifications;

public sealed class PaymentDeadlineReminderJob : IBackgroundJob
{
    private readonly IPendingBookingWorkSelector _workSelector;
    private readonly BookingNotificationService _notificationService;
    private readonly IBackgroundJobClaimManager _claimManager;
    private readonly IBookingReminderSchedule _schedule;
    private readonly IClock _clock;
    private readonly ILogger<PaymentDeadlineReminderJob> _logger;

    public PaymentDeadlineReminderJob(
        IPendingBookingWorkSelector workSelector,
        BookingNotificationService notificationService,
        IBackgroundJobClaimManager claimManager,
        IBookingReminderSchedule schedule,
        IClock clock,
        ILogger<PaymentDeadlineReminderJob> logger)
    {
        _workSelector = workSelector ??
            throw new ArgumentNullException(nameof(workSelector));

        _notificationService = notificationService ??
            throw new ArgumentNullException(nameof(notificationService));

        _claimManager = claimManager ??
            throw new ArgumentNullException(nameof(claimManager));

        _schedule = schedule ??
            throw new ArgumentNullException(nameof(schedule));

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
        DateTimeOffset windowStartUtc = nowUtc + _schedule.PaymentDeadlineLeadTime;
        DateTimeOffset windowEndUtc = windowStartUtc + _schedule.SelectionWindow;

        IReadOnlyList<Guid> bookingIds = await _workSelector.GetPaymentDeadlineReminderCandidateIdsAsync(
            windowStartUtc, windowEndUtc, batchSize, cancellationToken);

        int sentCount = 0;

        foreach (Guid bookingId in bookingIds)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                IBackgroundJobClaim? claim = await _claimManager.TryAcquireAsync(
                    BackgroundJobClaimKeys.PaymentDeadlineReminder,
                    bookingId,
                    cancellationToken);

                if (claim is null)
                {
                    continue;
                }

                await using (claim)
                {
                    Result<BookingNotificationDispatchOutcome> result = await _notificationService
                    .SendAsync(bookingId, BookingNotificationType.PaymentDeadlineReminder, cancellationToken);

                    if (result.IsFailure)
                    {
                        if (result.Error != BookingNotificationErrors.DeliveryDisabled)
                        {
                            _logger.LogWarning(
                                "Payment deadline reminder failed for booking {BookingId}. ErrorCode: {ErrorCode}.",
                                bookingId,
                                result.Error.Code);
                        }

                        continue;
                    }

                    if (result.Value == BookingNotificationDispatchOutcome.Sent)
                    {
                        sentCount++;
                    }
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
                    "Unexpected error sending payment deadline reminder for booking {BookingId}.",
                    bookingId);
            }
        }

        return sentCount;
    }
}
