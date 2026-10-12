namespace Bookify.Services.Booking.Application.Abstractions.BackgroundJobs;

public interface IBackgroundJobClaimManager
{
    Task<IBackgroundJobClaim?> TryAcquireAsync(
        string jobKey,
        Guid workItemId,
        CancellationToken cancellationToken = default);
}
