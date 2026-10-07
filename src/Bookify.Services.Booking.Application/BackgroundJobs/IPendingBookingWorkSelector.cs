namespace Bookify.Services.Booking.Application.BackgroundJobs;

public interface IPendingBookingWorkSelector
{
    Task<IReadOnlyList<Guid>> GetExpiredApprovalBookingIdsAsync(
        DateTimeOffset nowUtc,
        int batchSize,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Guid>> GetExpiredPaymentBookingIdsAsync(
        DateTimeOffset nowUtc,
        int batchSize,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Guid>> GetDueCompletionBookingIdsAsync(
        DateTimeOffset nowUtc,
        int batchSize,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Guid>> GetPaymentDeadlineReminderCandidateIdsAsync(
        DateTimeOffset windowStartUtc,
        DateTimeOffset windowEndUtc,
        int batchSize,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Guid>> GetUpcomingCheckInReminderCandidateIdsAsync(
        DateTimeOffset windowStartUtc,
        DateTimeOffset windowEndUtc,
        int batchSize,
        CancellationToken cancellationToken = default);
}
