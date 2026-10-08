using Bookify.Services.Booking.Application.Abstractions.BackgroundJobs;
using Microsoft.Extensions.Options;

namespace Bookify.Services.Booking.Api.BackgroundJobs;

public sealed class BookingBackgroundWorker : BackgroundService
{
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly ILogger<BookingBackgroundWorker> _logger;
    private readonly BackgroundJobsOptions _options;

    public BookingBackgroundWorker(
        IServiceScopeFactory serviceScopeFactory,
        IOptions<BackgroundJobsOptions> options,
        ILogger<BookingBackgroundWorker> logger)
    {
        _serviceScopeFactory = serviceScopeFactory ??
            throw new ArgumentNullException(nameof(serviceScopeFactory));

        ArgumentNullException.ThrowIfNull(options);

        _options = options.Value;
        _logger = logger ??
            throw new ArgumentNullException(nameof(logger));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation("Booking background worker is disabled.");
            return;
        }

        _logger.LogInformation("Booking background worker started. " +
            "Polling interval: {PollingInterval}. " +
            "Batch size: {BatchSize}",
            _options.PollingInterval,
            _options.BatchSize);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ExecuteCycleAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Unexpected error executing background work cycle.");
            }

            try
            {
                await Task.Delay(_options.PollingInterval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Booking background worker is stopping.");

        await base.StopAsync(cancellationToken);
    }

    private async Task ExecuteCycleAsync(CancellationToken cancellationToken)
    {
        await using AsyncServiceScope scope = _serviceScopeFactory.CreateAsyncScope();

        IEnumerable<IBackgroundJob> jobs = scope.ServiceProvider.GetServices<IBackgroundJob>();

        foreach (IBackgroundJob job in jobs)
        {
            cancellationToken.ThrowIfCancellationRequested();

            string jobName = job.GetType().Name;

            try
            {
                int processedCount = await job.ExecuteBatchAsync(_options.BatchSize, cancellationToken);

                if (processedCount > 0)
                {
                    _logger.LogInformation("Background job {JobName} processed {ProcessedCount} item(s).",
                        jobName,
                        processedCount);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Background job {JobName} failed.", jobName);
            }
        }
    }
}
