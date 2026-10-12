using System.Collections.Concurrent;
using Bookify.Services.Booking.Application.Abstractions.BackgroundJobs;
using Bookify.Services.Booking.Integration.Tests.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Bookify.Services.Booking.Integration.Tests.BackgroundJobs;

[Collection(BookingApiTestFixture.Name)]
[Trait("Category", "Integration")]
public sealed class BookingBackgroundWorkerTests
{
    private static readonly string[] JobKeys =
    [
        BackgroundJobClaimKeys.ApprovalExpiration,
        BackgroundJobClaimKeys.PaymentExpiration,
        BackgroundJobClaimKeys.BookingCompletion,
        BackgroundJobClaimKeys.OutboxNotification,
        BackgroundJobClaimKeys.PaymentDeadlineReminder,
        BackgroundJobClaimKeys.UpcomingCheckInReminder
    ];

    private readonly BookingApiFactory _factory;

    public BookingBackgroundWorkerTests(
        BookingApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Worker_WhenPollingContinues_ShouldCreateNewScopeForEachJobExecution()
    {
        CancellationToken cancellationToken =
            TestContext.Current.CancellationToken;

        // Six jobs execute during the first cycle.
        // Waiting for seven disposals proves that:
        //
        // 1. each job received its own scope;
        // 2. the worker started another polling cycle.
        var probe =
            new CycleProbe(
                requiredDisposals:
                    JobKeys.Length + 1);

        WebApplicationFactory<Program> workerFactory =
            _factory.WithWebHostBuilder(
                builder =>
                {
                    builder.UseSetting(
    "ConnectionStrings:Database",
    _factory.DatabaseConnectionString);

                    builder.UseSetting(
                        "BackgroundJobs:Enabled",
                        "true");

                    builder.UseSetting(
                        "BackgroundJobs:PollingInterval",
                        "00:00:00.100");

                    builder.UseSetting(
                        "BackgroundJobs:BatchSize",
                        "7");

                    builder.ConfigureTestServices(
                        services =>
                        {
                            services.RemoveAll<
                                IBackgroundJob>();

                            services.AddSingleton(
                                probe);

                            RegisterAllJobKeys<
                                ScopedProbeBackgroundJob>(
                                    services);
                        });
                });

        try
        {
            using HttpClient client =
                workerFactory.CreateClient();

            await probe.WaitForRequiredDisposalsAsync(
                cancellationToken);

            Assert.True(
                probe.DistinctInstanceCount >=
                JobKeys.Length + 1);

            Assert.All(
                probe.BatchSizes,
                batchSize =>
                    Assert.Equal(
                        7,
                        batchSize));

            Assert.True(
                probe.DisposalCount >=
                JobKeys.Length + 1);
        }
        finally
        {
            await workerFactory.DisposeAsync();
        }
    }

    [Fact]
    public async Task Worker_WhenOneJobThrows_ShouldContinueWithRemainingJobs()
    {
        CancellationToken cancellationToken =
            TestContext.Current.CancellationToken;

        var probe =
            new ExecutionProbe();

        WebApplicationFactory<Program> workerFactory =
            _factory.WithWebHostBuilder(
                builder =>
                {
                    builder.UseSetting(
    "ConnectionStrings:Database",
    _factory.DatabaseConnectionString);

                    builder.UseSetting(
                        "BackgroundJobs:Enabled",
                        "true");

                    builder.UseSetting(
                        "BackgroundJobs:PollingInterval",
                        "00:00:00.100");

                    builder.UseSetting(
                        "BackgroundJobs:BatchSize",
                        "5");

                    builder.ConfigureTestServices(
                        services =>
                        {
                            services.RemoveAll<
                                IBackgroundJob>();

                            services.AddSingleton(
                                probe);

                            services.AddKeyedScoped<
                                IBackgroundJob,
                                ThrowingBackgroundJob>(
                                BackgroundJobClaimKeys
                                    .ApprovalExpiration);

                            services.AddKeyedScoped<
                                IBackgroundJob,
                                SuccessfulBackgroundJob>(
                                BackgroundJobClaimKeys
                                    .PaymentExpiration);

                            services.AddKeyedScoped<
                                IBackgroundJob,
                                NoOpBackgroundJob>(
                                BackgroundJobClaimKeys
                                    .BookingCompletion);

                            services.AddKeyedScoped<
                                IBackgroundJob,
                                NoOpBackgroundJob>(
                                BackgroundJobClaimKeys
                                    .OutboxNotification);

                            services.AddKeyedScoped<
                                IBackgroundJob,
                                NoOpBackgroundJob>(
                                BackgroundJobClaimKeys
                                    .PaymentDeadlineReminder);

                            services.AddKeyedScoped<
                                IBackgroundJob,
                                NoOpBackgroundJob>(
                                BackgroundJobClaimKeys
                                    .UpcomingCheckInReminder);
                        });
                });

        try
        {
            using HttpClient client =
                workerFactory.CreateClient();

            await probe.WaitForExecutionAsync(
                cancellationToken);

            Assert.True(
                probe.ExecutionObserved);
        }
        finally
        {
            await workerFactory.DisposeAsync();
        }
    }

    [Fact]
    public async Task Worker_WhenHostStops_ShouldPropagateCancellationToRunningJob()
    {
        CancellationToken cancellationToken =
            TestContext.Current.CancellationToken;

        var probe =
            new CancellationProbe();

        WebApplicationFactory<Program> workerFactory =
            _factory.WithWebHostBuilder(
                builder =>
                {
                    builder.UseSetting(
    "ConnectionStrings:Database",
    _factory.DatabaseConnectionString);

                    builder.UseSetting(
                        "BackgroundJobs:Enabled",
                        "true");

                    builder.UseSetting(
                        "BackgroundJobs:PollingInterval",
                        "00:00:10");

                    builder.UseSetting(
                        "BackgroundJobs:BatchSize",
                        "5");

                    builder.ConfigureTestServices(
                        services =>
                        {
                            services.RemoveAll<
                                IBackgroundJob>();

                            services.AddSingleton(
                                probe);

                            services.AddKeyedScoped<
                                IBackgroundJob,
                                BlockingBackgroundJob>(
                                BackgroundJobClaimKeys
                                    .ApprovalExpiration);

                            services.AddKeyedScoped<
                                IBackgroundJob,
                                NoOpBackgroundJob>(
                                BackgroundJobClaimKeys
                                    .PaymentExpiration);

                            services.AddKeyedScoped<
                                IBackgroundJob,
                                NoOpBackgroundJob>(
                                BackgroundJobClaimKeys
                                    .BookingCompletion);

                            services.AddKeyedScoped<
                                IBackgroundJob,
                                NoOpBackgroundJob>(
                                BackgroundJobClaimKeys
                                    .OutboxNotification);

                            services.AddKeyedScoped<
                                IBackgroundJob,
                                NoOpBackgroundJob>(
                                BackgroundJobClaimKeys
                                    .PaymentDeadlineReminder);

                            services.AddKeyedScoped<
                                IBackgroundJob,
                                NoOpBackgroundJob>(
                                BackgroundJobClaimKeys
                                    .UpcomingCheckInReminder);
                        });
                });

        try
        {
            using HttpClient client =
                workerFactory.CreateClient();

            await probe.WaitForStartAsync(
                cancellationToken);

            IHostApplicationLifetime lifetime =
                workerFactory.Services
                    .GetRequiredService<
                        IHostApplicationLifetime>();

            lifetime.StopApplication();

            await probe.WaitForCancellationAsync(
                cancellationToken);

            Assert.True(
                probe.CancellationObserved);
        }
        finally
        {
            await workerFactory.DisposeAsync();
        }
    }

    private static void RegisterAllJobKeys<TJob>(
        IServiceCollection services)
        where TJob : class, IBackgroundJob
    {
        foreach (string jobKey in JobKeys)
        {
            services.AddKeyedScoped<
                IBackgroundJob,
                TJob>(
                jobKey);
        }
    }

    private sealed class CycleProbe
    {
        private readonly int _requiredDisposals;

        private readonly ConcurrentDictionary<
            Guid,
            byte> _instances = new();

        private readonly ConcurrentQueue<int>
            _batchSizes = new();

        private readonly TaskCompletionSource<bool>
            _completed = new(
                TaskCreationOptions
                    .RunContinuationsAsynchronously);

        private int _disposalCount;

        public CycleProbe(
            int requiredDisposals)
        {
            _requiredDisposals =
                requiredDisposals;
        }

        public int DistinctInstanceCount =>
            _instances.Count;

        public int DisposalCount =>
            Volatile.Read(
                ref _disposalCount);

        public IReadOnlyCollection<int>
            BatchSizes =>
                _batchSizes.ToArray();

        public void RecordExecution(
            Guid instanceId,
            int batchSize)
        {
            _instances.TryAdd(
                instanceId,
                0);

            _batchSizes.Enqueue(
                batchSize);
        }

        public void RecordDisposal()
        {
            int count =
                Interlocked.Increment(
                    ref _disposalCount);

            if (count >=
                _requiredDisposals)
            {
                _completed.TrySetResult(
                    true);
            }
        }

        public Task<bool> WaitForRequiredDisposalsAsync(
            CancellationToken cancellationToken)
        {
            return _completed.Task.WaitAsync(
                TimeSpan.FromSeconds(5),
                cancellationToken);
        }
    }

    private sealed class ScopedProbeBackgroundJob
        : IBackgroundJob,
          IDisposable
    {
        private readonly Guid _instanceId =
            Guid.NewGuid();

        private readonly CycleProbe _probe;

        public ScopedProbeBackgroundJob(
            CycleProbe probe)
        {
            _probe = probe;
        }

        public Task<int> ExecuteBatchAsync(
            int batchSize,
            CancellationToken cancellationToken = default)
        {
            cancellationToken
                .ThrowIfCancellationRequested();

            _probe.RecordExecution(
                _instanceId,
                batchSize);

            return Task.FromResult(
                0);
        }

        public void Dispose()
        {
            _probe.RecordDisposal();
        }
    }

    private sealed class ExecutionProbe
    {
        private readonly TaskCompletionSource<bool>
            _executed = new(
                TaskCreationOptions
                    .RunContinuationsAsynchronously);

        public bool ExecutionObserved
        {
            get;
            private set;
        }

        public void Record()
        {
            ExecutionObserved = true;

            _executed.TrySetResult(
                true);
        }

        public Task<bool> WaitForExecutionAsync(
            CancellationToken cancellationToken)
        {
            return _executed.Task.WaitAsync(
                TimeSpan.FromSeconds(5),
                cancellationToken);
        }
    }

    private sealed class ThrowingBackgroundJob
        : IBackgroundJob
    {
        public Task<int> ExecuteBatchAsync(
            int batchSize,
            CancellationToken cancellationToken = default)
        {
            cancellationToken
                .ThrowIfCancellationRequested();

            throw new InvalidOperationException(
                "Expected test failure.");
        }
    }

    private sealed class SuccessfulBackgroundJob
        : IBackgroundJob
    {
        private readonly ExecutionProbe _probe;

        public SuccessfulBackgroundJob(
            ExecutionProbe probe)
        {
            _probe = probe;
        }

        public Task<int> ExecuteBatchAsync(
            int batchSize,
            CancellationToken cancellationToken = default)
        {
            cancellationToken
                .ThrowIfCancellationRequested();

            _probe.Record();

            return Task.FromResult(
                1);
        }
    }

    private sealed class NoOpBackgroundJob
        : IBackgroundJob
    {
        public Task<int> ExecuteBatchAsync(
            int batchSize,
            CancellationToken cancellationToken = default)
        {
            cancellationToken
                .ThrowIfCancellationRequested();

            return Task.FromResult(
                0);
        }
    }

    private sealed class CancellationProbe
    {
        private readonly TaskCompletionSource<bool>
            _started = new(
                TaskCreationOptions
                    .RunContinuationsAsynchronously);

        private readonly TaskCompletionSource<bool>
            _cancelled = new(
                TaskCreationOptions
                    .RunContinuationsAsynchronously);

        public bool CancellationObserved
        {
            get;
            private set;
        }

        public void RecordStarted()
        {
            _started.TrySetResult(
                true);
        }

        public void RecordCancellation()
        {
            CancellationObserved = true;

            _cancelled.TrySetResult(
                true);
        }

        public Task<bool> WaitForStartAsync(
            CancellationToken cancellationToken)
        {
            return _started.Task.WaitAsync(
                TimeSpan.FromSeconds(5),
                cancellationToken);
        }

        public Task<bool> WaitForCancellationAsync(
            CancellationToken cancellationToken)
        {
            return _cancelled.Task.WaitAsync(
                TimeSpan.FromSeconds(5),
                cancellationToken);
        }
    }

    private sealed class BlockingBackgroundJob
        : IBackgroundJob
    {
        private readonly CancellationProbe _probe;

        public BlockingBackgroundJob(
            CancellationProbe probe)
        {
            _probe = probe;
        }

        public async Task<int> ExecuteBatchAsync(
            int batchSize,
            CancellationToken cancellationToken = default)
        {
            _probe.RecordStarted();

            try
            {
                await Task.Delay(
                    Timeout.InfiniteTimeSpan,
                    cancellationToken);
            }
            catch (OperationCanceledException)
                when (cancellationToken.IsCancellationRequested)
            {
                _probe.RecordCancellation();

                throw;
            }

            return 0;
        }
    }
}
