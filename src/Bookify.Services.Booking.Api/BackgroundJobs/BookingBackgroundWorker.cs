using Bookify.Services.Booking.Api.Observability;
using Bookify.Services.Booking.Application.Abstractions.BackgroundJobs;
using Bookify.Services.Booking.Application.Observability;
using Microsoft.Extensions.Options;
using System.Diagnostics;

namespace Bookify.Services.Booking.Api.BackgroundJobs;

public sealed class BookingBackgroundWorker : BackgroundService
{
    private static readonly ActivitySource JobActivitySource =
        new("Bookify.Services.Booking.BackgroundJobs");

    private static readonly string[] JobKeys =
    [
        BackgroundJobClaimKeys.ApprovalExpiration,
        BackgroundJobClaimKeys.PaymentExpiration,
        BackgroundJobClaimKeys.BookingCompletion,
        BackgroundJobClaimKeys.OutboxNotification,
        BackgroundJobClaimKeys.PaymentDeadlineReminder,
        BackgroundJobClaimKeys.UpcomingCheckInReminder
    ];

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
            _logger.LogInformation(
                ApiLogEvents.BackgroundWorkerDisabled,
                "Booking background worker is disabled.");
            return;
        }

        _logger.LogInformation(
            ApiLogEvents.BackgroundWorkerStarted,
            "Booking background worker started. " +
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
                _logger.LogError(
                    ApiLogEvents.BackgroundWorkerCycleFailed,
                    exception,
                    "Unexpected error executing background work cycle.");
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
        _logger.LogInformation(
            ApiLogEvents.BackgroundWorkerStopping,
            "Booking background worker is stopping.");

        await base.StopAsync(cancellationToken);
    }

    private async Task ExecuteCycleAsync(CancellationToken cancellationToken)
    {
        foreach (string jobKey in JobKeys)
        {
            cancellationToken.ThrowIfCancellationRequested();

            await using AsyncServiceScope jobScope = _serviceScopeFactory.CreateAsyncScope();

            IBackgroundJob job = jobScope.ServiceProvider
                .GetRequiredKeyedService<IBackgroundJob>(jobKey);

            string jobName = job.GetType().Name;

            using Activity? activity = JobActivitySource.StartActivity(
                "background_job.execute",
                ActivityKind.Internal);

            activity?.SetTag("bookify.job.name", jobName);
            activity?.SetTag("bookify.job.key", jobKey);

            try
            {
                int processedCount = await job.ExecuteBatchAsync(_options.BatchSize, cancellationToken);

                activity?.SetTag("bookify.job.processed_count", processedCount);

                BookingMetrics.RecordJobProcessed(jobName, processedCount);

                if (processedCount > 0)
                {
                    _logger.LogInformation(
                        ApiLogEvents.BackgroundJobProcessed,
                        "Background job {JobName} processed {ProcessedCount} item(s).",
                        jobName,
                        processedCount);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                activity?.SetStatus(ActivityStatusCode.Unset);
                throw;
            }
            catch (Exception exception)
            {
                activity?.SetStatus(ActivityStatusCode.Error, "Background job execution failed");

                BookingMetrics.RecordJobFailed(jobName);

                _logger.LogError(
                    ApiLogEvents.BackgroundJobFailed,
                    exception,
                    "Background job {JobName} failed.",
                    jobName);
            }
        }
    }
}
