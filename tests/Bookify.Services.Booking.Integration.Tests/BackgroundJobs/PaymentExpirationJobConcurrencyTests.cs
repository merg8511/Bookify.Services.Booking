
using Bookify.Services.Booking.Application.Abstractions.BackgroundJobs;
using Bookify.Services.Booking.Application.Abstractions.Messaging;
using Bookify.Services.Booking.Application.Abstractions.Time;
using Bookify.Services.Booking.Application.BackgroundJobs;
using Bookify.Services.Booking.Application.BackgroundJobs.Expiration;
using Bookify.Services.Booking.Application.Bookings.ExpirePayment;
using Bookify.Services.Booking.Domain.Shared;
using Bookify.Services.Booking.Integration.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Bookify.Services.Booking.Integration.Tests.BackgroundJobs;

[Collection(BookingApiTestFixture.Name)]
[Trait("Category", "Integration")]
[Trait("Category", "Concurrency")]
public sealed class PaymentExpirationJobConcurrencyTests
{
    private readonly BookingApiFactory _factory;

    public PaymentExpirationJobConcurrencyTests(
        BookingApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task TwoWorkers_WithSameBooking_ShouldExecuteCommandOnlyOnceWhileClaimHeld()
    {
        // ARRANGE
        CancellationToken cancellationToken =
            TestContext.Current.CancellationToken;

        Guid bookingId = Guid.NewGuid();

        var selector = new SingleBookingWorkSelector(bookingId);

        // Este executor permite conservar ocupado el primer
        // worker mientras el segundo intenta procesar el mismo ID.
        var executor = new BlockingExpirationExecutor();

        using IServiceScope firstScope =
            _factory.Services.CreateScope();

        using IServiceScope secondScope =
            _factory.Services.CreateScope();

        IBackgroundJobClaimManager firstClaimManager =
            firstScope.ServiceProvider
                .GetRequiredService<IBackgroundJobClaimManager>();

        IBackgroundJobClaimManager secondClaimManager =
            secondScope.ServiceProvider
                .GetRequiredService<IBackgroundJobClaimManager>();

        IClock clock =
            firstScope.ServiceProvider.GetRequiredService<IClock>();

        var firstJob = new PaymentExpirationJob(
            selector,
            executor,
            firstClaimManager,
            clock,
            NullLogger<PaymentExpirationJob>.Instance);

        var secondJob = new PaymentExpirationJob(
            selector,
            executor,
            secondClaimManager,
            clock,
            NullLogger<PaymentExpirationJob>.Instance);

        // ACT
        Task<int> firstExecution =
            firstJob.ExecuteBatchAsync(
                1,
                cancellationToken);

        try
        {
            // Esperamos hasta que el primer worker ENTRE al
            // comando. En ese punto ya obtuvo el advisory claim.
            await executor.FirstExecutionEntered.WaitAsync(
                TimeSpan.FromSeconds(10),
                cancellationToken);

            // El segundo worker encuentra el mismo work item,
            // pero el claim sigue ocupado.
            int secondProcessed =
                await secondJob.ExecuteBatchAsync(
                    1,
                    cancellationToken);

            // ASSERT — segundo worker no ejecutó el comando.
            Assert.Equal(0, secondProcessed);

            Assert.Equal(
                1,
                executor.ExecutionCount);
        }
        finally
        {
            // Liberamos al primer worker incluso si una
            // aserción falla, evitando dejar su tarea bloqueada.
            executor.Release();
        }

        int firstProcessed =
            await firstExecution;

        // ASSERT — solo hubo una ejecución concurrente.
        Assert.Equal(1, firstProcessed);

        Assert.Equal(
            1,
            executor.ExecutionCount);

        Assert.Equal(
            bookingId,
            executor.ExecutedBookingId);
    }

    private sealed class BlockingExpirationExecutor :
        ICommandExecutor<ExpireBookingPaymentCommand>
    {
        private readonly TaskCompletionSource<bool> _entered =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        private readonly TaskCompletionSource<bool> _release =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        private int _executionCount;

        public Task FirstExecutionEntered => _entered.Task;

        public int ExecutionCount =>
            Volatile.Read(ref _executionCount);

        public Guid? ExecutedBookingId { get; private set; }

        public async Task<Result> ExecuteAsync(
            ExpireBookingPaymentCommand command,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            Interlocked.Increment(ref _executionCount);

            ExecutedBookingId = command.BookingId;

            // Señal: el primer worker ya obtuvo su claim
            // y ahora está dentro de la ejecución.
            _entered.TrySetResult(true);

            // Se mantiene el claim hasta que el test
            // autorice la finalización.
            await _release.Task.WaitAsync(cancellationToken);

            return Result.Success();
        }

        public void Release()
        {
            _release.TrySetResult(true);
        }
    }

    private sealed class SingleBookingWorkSelector :
        IPendingBookingWorkSelector
    {
        private readonly Guid _bookingId;

        public SingleBookingWorkSelector(Guid bookingId)
        {
            _bookingId = bookingId;
        }

        public Task<IReadOnlyList<Guid>>
            GetExpiredPaymentBookingIdsAsync(
                DateTimeOffset nowUtc,
                int batchSize,
                CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            IReadOnlyList<Guid> ids = [_bookingId];

            return Task.FromResult(ids);
        }

        public Task<IReadOnlyList<Guid>>
            GetExpiredApprovalBookingIdsAsync(
                DateTimeOffset nowUtc,
                int batchSize,
                CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<IReadOnlyList<Guid>>
            GetDueCompletionBookingIdsAsync(
                DateTimeOffset nowUtc,
                int batchSize,
                CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<IReadOnlyList<Guid>>
            GetPaymentDeadlineReminderCandidateIdsAsync(
                DateTimeOffset windowStartUtc,
                DateTimeOffset windowEndUtc,
                int batchSize,
                CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<IReadOnlyList<Guid>>
            GetUpcomingCheckInReminderCandidateIdsAsync(
                DateTimeOffset windowStartUtc,
                DateTimeOffset windowEndUtc,
                int batchSize,
                CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }
}
