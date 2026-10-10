using Bookify.Services.Booking.Application.Abstractions.BackgroundJobs;
using Bookify.Services.Booking.Application.Abstractions.Time;
using Bookify.Services.Booking.Application.Notifications;
using Bookify.Services.Booking.Domain.Shared;
using Microsoft.Extensions.Logging;

namespace Bookify.Services.Booking.Application.BackgroundJobs.Notifications;

public sealed class UpcomingCheckInReminderJob : IBackgroundJob
{
    private readonly IPendingBookingWorkSelector _workSelector;
    private readonly BookingNotificationService _notificationService;
    private readonly IBookingReminderSchedule _schedule;
    private readonly IClock _clock;
    private readonly ILogger<UpcomingCheckInReminderJob> _logger;

    public UpcomingCheckInReminderJob(
        IPendingBookingWorkSelector workSelector,
        BookingNotificationService notificationService,
        IBookingReminderSchedule schedule,
        IClock clock,
        ILogger<UpcomingCheckInReminderJob> logger)
    {
        _workSelector = workSelector ??
            throw new ArgumentNullException(nameof(workSelector));

        _notificationService = notificationService ??
            throw new ArgumentNullException(nameof(notificationService));

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
        DateTimeOffset windowStartUtc = nowUtc + _schedule.UpcomingCheckInLeadTime;
        DateTimeOffset windowEndUtc = windowStartUtc + _schedule.SelectionWindow;

        IReadOnlyList<Guid> bookingIds = await _workSelector
            .GetUpcomingCheckInReminderCandidateIdsAsync(windowStartUtc, windowEndUtc, batchSize, cancellationToken);

        int sentCount = 0;

        foreach (Guid bookingId in bookingIds)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                Result<BookingNotificationDispatchOutcome> result = await _notificationService
                    .SendAsync(bookingId, BookingNotificationType.UpcomingCheckInReminder, cancellationToken);

                if (result.IsFailure)
                {
                    if (result.Error != BookingNotificationErrors.DeliveryDisabled)
                    {
                        _logger.LogWarning(
                            "Upcoming check-in reminder failed for booking {BookingId}. ErrorCode: {ErrorCode}.",
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
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                _logger.LogError(
                    exception,
                    "Unexpected error sending upcoming check-in reminder for booking {BookingId}.",
                    bookingId);
            }
        }

        return sentCount;
    }
}
