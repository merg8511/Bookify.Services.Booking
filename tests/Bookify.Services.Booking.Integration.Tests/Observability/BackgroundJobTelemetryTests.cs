
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using Bookify.Services.Booking.Api.BackgroundJobs;
using Bookify.Services.Booking.Application.Abstractions.BackgroundJobs;
using Bookify.Services.Booking.Application.Observability;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Bookify.Services.Booking.Integration.Tests.Observability;

public sealed class BackgroundJobTelemetryTests
{
    private const string JobActivitySource =
        "Bookify.Services.Booking.BackgroundJobs";

    private static readonly string[] JobKeys =
    [
        BackgroundJobClaimKeys.ApprovalExpiration,
        BackgroundJobClaimKeys.PaymentExpiration,
        BackgroundJobClaimKeys.BookingCompletion,
        BackgroundJobClaimKeys.OutboxNotification,
        BackgroundJobClaimKeys.PaymentDeadlineReminder,
        BackgroundJobClaimKeys.UpcomingCheckInReminder
    ];

    [Fact]
    public async Task Worker_ShouldRecordProcessedItemsAndJobActivity()
    {
        CancellationToken cancellationToken =
            TestContext.Current.CancellationToken;

        using var capture = new TelemetryCapture();

        using ServiceProvider provider =
            CreateJobProvider(
                failingJobKey: null,
                processedJobKey:
                    BackgroundJobClaimKeys.ApprovalExpiration,
                processedCount: 3);

        using var worker =
            CreateWorker(provider);

        await worker.StartAsync(cancellationToken);

        try
        {
            await capture.WaitForProcessedAsync(
                cancellationToken);

            await capture.WaitForActivityStoppedAsync(
                cancellationToken);

            Assert.Contains(
                capture.Measurements,
                measurement =>
                    measurement.Instrument ==
                        "bookify.jobs.processed" &&
                    measurement.Value == 3 &&
                    measurement.JobName ==
                        nameof(TestBackgroundJob));

            Assert.Contains(
                capture.StoppedActivities,
                activity =>
                    activity.OperationName ==
                        "background_job.execute" &&
                    activity.Kind == ActivityKind.Internal &&
                    activity.Tags.Any(tag =>
                        tag.Key == "bookify.job.processed_count" &&
                        tag.Value == "3"));
        }
        finally
        {
            await worker.StopAsync(
                CancellationToken.None);
        }
    }

    [Fact]
    public async Task Worker_WhenJobThrows_ShouldRecordFailureAndContinue()
    {
        CancellationToken cancellationToken =
            TestContext.Current.CancellationToken;

        using var capture = new TelemetryCapture();

        using ServiceProvider provider =
            CreateJobProvider(
                failingJobKey:
                    BackgroundJobClaimKeys.ApprovalExpiration,
                processedJobKey:
                    BackgroundJobClaimKeys.PaymentExpiration,
                processedCount: 2);

        using var worker =
            CreateWorker(provider);

        await worker.StartAsync(cancellationToken);

        try
        {
            await capture.WaitForFailedAndProcessedAsync(
                cancellationToken);

            await capture.WaitForActivityStoppedAsync(
                cancellationToken);

            Assert.Contains(
                capture.Measurements,
                measurement =>
                    measurement.Instrument ==
                        "bookify.jobs.failed" &&
                    measurement.Value == 1);

            Assert.Contains(
                capture.Measurements,
                measurement =>
                    measurement.Instrument ==
                        "bookify.jobs.processed" &&
                    measurement.Value == 2);

            Assert.Contains(
                capture.StoppedActivities,
                activity =>
                    activity.OperationName ==
                        "background_job.execute" &&
                    activity.Status == ActivityStatusCode.Error);
        }
        finally
        {
            await worker.StopAsync(
                CancellationToken.None);
        }
    }

    private static ServiceProvider CreateJobProvider(
        string? failingJobKey,
        string processedJobKey,
        int processedCount)
    {
        var services = new ServiceCollection();

        foreach (string jobKey in JobKeys)
        {
            string currentKey = jobKey;

            services.AddKeyedScoped<IBackgroundJob>(
                currentKey,
                (_, _) => new TestBackgroundJob(
                    shouldFail: currentKey == failingJobKey,
                    processedCount:
                        currentKey == processedJobKey
                            ? processedCount
                            : 0));
        }

        return services.BuildServiceProvider();
    }

    private static BookingBackgroundWorker CreateWorker(
        ServiceProvider provider)
    {
        return new BookingBackgroundWorker(
            provider.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(
                new BackgroundJobsOptions
                {
                    Enabled = true,
                    PollingInterval = TimeSpan.FromMinutes(1),
                    BatchSize = 5
                }),
            NullLogger<BookingBackgroundWorker>.Instance);
    }

    private sealed class TestBackgroundJob : IBackgroundJob
    {
        private readonly bool _shouldFail;
        private readonly int _processedCount;

        public TestBackgroundJob(
            bool shouldFail,
            int processedCount)
        {
            _shouldFail = shouldFail;
            _processedCount = processedCount;
        }

        public Task<int> ExecuteBatchAsync(
            int batchSize,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (_shouldFail)
            {
                throw new InvalidOperationException(
                    "Simulated background job failure.");
            }

            return Task.FromResult(_processedCount);
        }
    }

    private sealed record CapturedMeasurement(
        string Instrument,
        long Value,
        string? JobName);

    private sealed record CapturedActivity(
        string OperationName,
        ActivityKind Kind,
        ActivityStatusCode Status,
        IReadOnlyList<KeyValuePair<string, string?>> Tags);

    private sealed class TelemetryCapture : IDisposable
    {
        private readonly ConcurrentQueue<CapturedMeasurement>
            _measurements = new();

        private readonly ConcurrentQueue<CapturedActivity>
            _activities = new();

        private readonly MeterListener _meterListener;
        private readonly ActivityListener _activityListener;

        private readonly TaskCompletionSource<bool>
    _activityStopped = new(
        TaskCreationOptions.RunContinuationsAsynchronously);

        private readonly TaskCompletionSource<bool>
            _processed = new(
                TaskCreationOptions.RunContinuationsAsynchronously);

        private readonly TaskCompletionSource<bool>
            _failedAndProcessed = new(
                TaskCreationOptions.RunContinuationsAsynchronously);

        private int _hasFailure;
        private int _hasProcessing;

        public TelemetryCapture()
        {
            _meterListener = new MeterListener
            {
                InstrumentPublished = (instrument, listener) =>
                {
                    if (instrument.Meter.Name ==
                            BookingMetrics.MeterName &&
                        instrument.Name is
                            "bookify.jobs.processed" or
                            "bookify.jobs.failed")
                    {
                        listener.EnableMeasurementEvents(instrument);
                    }
                }
            };

            _meterListener.SetMeasurementEventCallback<long>(
                (instrument, value, tags, _) =>
                {
                    string? jobName = null;

                    foreach (KeyValuePair<string, object?> tag
                        in tags)
                    {
                        if (tag.Key == "job.name")
                        {
                            jobName = tag.Value?.ToString();
                        }
                    }

                    _measurements.Enqueue(
                        new CapturedMeasurement(
                            instrument.Name,
                            value,
                            jobName));

                    if (instrument.Name == "bookify.jobs.processed"
                        && value > 0)
                    {
                        Interlocked.Exchange(ref _hasProcessing, 1);
                        _processed.TrySetResult(true);
                    }

                    if (instrument.Name == "bookify.jobs.failed")
                    {
                        Interlocked.Exchange(ref _hasFailure, 1);
                    }

                    if (Volatile.Read(ref _hasFailure) == 1 &&
                        Volatile.Read(ref _hasProcessing) == 1)
                    {
                        _failedAndProcessed.TrySetResult(true);
                    }
                });

            _meterListener.Start();

            _activityListener = new ActivityListener
            {
                ShouldListenTo = source =>
                    source.Name == JobActivitySource,

                Sample =
                    (ref ActivityCreationOptions<ActivityContext> _) =>
                        ActivitySamplingResult.AllDataAndRecorded,

                SampleUsingParentId =
                    (ref ActivityCreationOptions<string> _) =>
                        ActivitySamplingResult.AllDataAndRecorded,

                ActivityStopped = activity =>
                {
                    _activities.Enqueue(
                        new CapturedActivity(
                            activity.OperationName,
                            activity.Kind,
                            activity.Status,
                            activity.TagObjects
                                .Select(tag =>
                                    new KeyValuePair<string, string?>(
                                        tag.Key,
                                        tag.Value?.ToString()))
                                .ToArray()));

                    _activityStopped.TrySetResult(true);
                }
            };

            ActivitySource.AddActivityListener(_activityListener);
        }

        public IReadOnlyList<CapturedMeasurement> Measurements =>
            _measurements.ToArray();

        public IReadOnlyList<CapturedActivity> StoppedActivities =>
            _activities.ToArray();

        public Task<bool> WaitForProcessedAsync(
            CancellationToken cancellationToken)
        {
            return _processed.Task.WaitAsync(
                TimeSpan.FromSeconds(5),
                cancellationToken);
        }

        public Task<bool> WaitForFailedAndProcessedAsync(
            CancellationToken cancellationToken)
        {
            return _failedAndProcessed.Task.WaitAsync(
                TimeSpan.FromSeconds(5),
                cancellationToken);
        }

        public void Dispose()
        {
            _activityListener.Dispose();
            _meterListener.Dispose();
        }

        public Task<bool> WaitForActivityStoppedAsync(
    CancellationToken cancellationToken)
        {
            return _activityStopped.Task.WaitAsync(
                TimeSpan.FromSeconds(5),
                cancellationToken);
        }
    }
}
