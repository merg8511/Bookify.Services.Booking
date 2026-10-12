namespace Bookify.Services.Booking.Application.Abstractions.BackgroundJobs;

public interface IBackgroundJob
{
    Task<int> ExecuteBatchAsync(
        int batchSize,
        CancellationToken cancellationToken = default);
}
