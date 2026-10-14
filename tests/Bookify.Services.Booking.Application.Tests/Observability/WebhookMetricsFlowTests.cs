using Bookify.Services.Booking.Application.Abstractions.Payments;
using Bookify.Services.Booking.Application.Abstractions.Payments.Webhooks;
using Bookify.Services.Booking.Application.Abstractions.Persistence;
using Bookify.Services.Booking.Application.Abstractions.Persistence.Repositories;
using Bookify.Services.Booking.Application.Observability;
using Bookify.Services.Booking.Application.Payments.Initiate;
using Bookify.Services.Booking.Application.Payments.Webhooks.Stripe;
using Bookify.Services.Booking.Application.Tests.Infrastructure;
using Bookify.Services.Booking.Domain.Payments;
using Bookify.Services.Booking.Domain.Shared;
using System.Collections.Concurrent;
using System.Diagnostics.Metrics;
using System.Text.Json;
using DomainBooking = Bookify.Services.Booking.Domain.Bookings.Booking;

namespace Bookify.Services.Booking.Application.Tests.Observability;

[Collection("Bookify Metrics Flow")]
public sealed class WebhookMetricsFlowTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 9, 6, 19, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task AlreadyProcessedEvent_ShouldCountDuplicateButNotPaymentOutcome()
    {
        using var capture = new WebhookMetricCapture();
        var (handler, booking, attempt) = CreateHandler(
            PaymentWebhookPreparationStatus.AlreadyProcessed);

        Result result = await handler.HandleAsync(
            new ProcessStripeWebhookCommand(
                Payload("payment_intent.succeeded", booking.Id, attempt.ExternalReference),
                "t=1,v1=test"),
            TestContext.Current.CancellationToken);

        Assert.Empty(capture.PaymentTransitions);
        Assert.True(result.IsSuccess);
        Assert.Equal(1, capture.Duplicates);
        Assert.Empty(capture.PaymentStatuses);
    }

    [Theory]
    [InlineData("payment_intent.succeeded", "succeeded")]
    [InlineData("payment_intent.payment_failed", "failed")]
    public async Task ReconciledEvent_ShouldRecordPaymentOutcome(
        string eventType, string expectedStatus)
    {
        using var capture = new WebhookMetricCapture();
        var (handler, booking, attempt) = CreateHandler(
            PaymentWebhookPreparationStatus.ReadyToProcess);

        Result result = await handler.HandleAsync(
            new ProcessStripeWebhookCommand(
                Payload(eventType, booking.Id, attempt.ExternalReference),
                "t=1,v1=test"),
            TestContext.Current.CancellationToken);

        Assert.Equal(expectedStatus, Assert.Single(capture.PaymentTransitions));
        Assert.True(result.IsSuccess);
        Assert.Equal(0, capture.Duplicates);
        Assert.Equal(expectedStatus, Assert.Single(capture.PaymentStatuses));
    }

    [Fact]
    public async Task InvalidSignature_ShouldNotEmitBusinessMetrics()
    {
        using var capture = new WebhookMetricCapture();
        var (handler, booking, attempt) = CreateHandler(
            PaymentWebhookPreparationStatus.ReadyToProcess,
            signatureValid: false);

        Result result = await handler.HandleAsync(
            new ProcessStripeWebhookCommand(
                Payload("payment_intent.succeeded", booking.Id, attempt.ExternalReference),
                "t=1,v1=invalid"),
            TestContext.Current.CancellationToken);

        Assert.Empty(capture.PaymentTransitions);
        Assert.True(result.IsFailure);
        Assert.Equal(0, capture.Duplicates);
        Assert.Empty(capture.PaymentStatuses);
    }

    private static (ProcessStripeWebhookCommandHandler Handler,
        DomainBooking Booking, PaymentAttempt Attempt) CreateHandler(
        PaymentWebhookPreparationStatus status,
        bool signatureValid = true)
    {
        DomainBooking booking = BookingTestFactory.CreatePendingPaymentBooking();
        (Payment payment, PaymentAttempt attempt) = BookingTestFactory.CreatePendingPayment(
            booking, $"pi_{Guid.NewGuid():N}", 200m, "USD", Now.AddMinutes(-2));

        var handler = new ProcessStripeWebhookCommandHandler(
            new StubBookingRepository(booking),
            new StubPaymentRepository(payment),
            new TestSignatureVerifier(signatureValid),
            new FakeWebhookStore(status),
            new SuccessfulPaymentInitiationLock(),
            new NoOpUnitOfWork(),
            new FakeTransactionManager(),
            new FixedClock(Now));

        return (handler, booking, attempt);
    }

    private static string Payload(string eventType, Guid bookingId, string reference) =>
        JsonSerializer.Serialize(new
        {
            id = $"evt_{Guid.NewGuid():N}",
            type = eventType,
            data = new
            {
                @object = new
                {
                    id = reference,
                    metadata = new Dictionary<string, string>
                    {
                        ["bookify_booking_id"] = bookingId.ToString("D")
                    }
                }
            }
        });

    private sealed class TestSignatureVerifier : IStripeWebhookSignatureVerifier
    {
        private readonly bool _valid;
        public TestSignatureVerifier(bool valid) => _valid = valid;
        public Result Verify(string rawBody, string signatureHeader, DateTimeOffset utcNow) =>
            _valid ? Result.Success() : Result.Failure(StripeWebhookSignatureErrors.InvalidSignature);
    }

    private sealed class FakeWebhookStore : IPaymentWebhookEventStore
    {
        private readonly PaymentWebhookPreparationStatus _status;
        public FakeWebhookStore(PaymentWebhookPreparationStatus status) => _status = status;

        public Task<Result<PaymentWebhookEventPreparation>> PrepareAsync(
            string provider, string eventId, string eventType,
            DateTimeOffset receivedAtUtc, CancellationToken cancellationToken = default) =>
            Task.FromResult(Result<PaymentWebhookEventPreparation>.Success(
                new PaymentWebhookEventPreparation(Guid.NewGuid(), _status)));

        public Task MarkProcessedAsync(Guid eventRecordId, DateTimeOffset processedAtUtc,
            CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task MarkFailedAsync(Guid eventRecordId, Error error,
            CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<StoredPaymentWebhookEvent?> GetAsync(
            string provider, string eventId, CancellationToken cancellationToken = default) =>
            Task.FromResult<StoredPaymentWebhookEvent?>(null);
    }

    private sealed class StubBookingRepository : IBookingRepository
    {
        private readonly DomainBooking _booking;
        public StubBookingRepository(DomainBooking booking) => _booking = booking;
        public Task<DomainBooking?> GetByIdAsync(Guid bookingId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<DomainBooking?>(_booking.Id == bookingId ? _booking : null);
        public void Add(DomainBooking booking) => throw new NotSupportedException();
    }

    private sealed class StubPaymentRepository : IPaymentRepository
    {
        private readonly Payment _payment;
        public StubPaymentRepository(Payment payment) => _payment = payment;
        public Task<Payment?> GetByBookingIdAsync(Guid bookingId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<Payment?>(_payment.BookingId == bookingId ? _payment : null);
        public void Add(Payment payment) => throw new NotSupportedException();
    }

    private sealed class SuccessfulPaymentInitiationLock : IPaymentInitiationLock
    {
        public Task<bool> TryAcquireAsync(Guid bookingId,
            CancellationToken cancellationToken = default) => Task.FromResult(true);
    }

    private sealed class NoOpUnitOfWork : IUnitOfWork
    {
        public Task SaveChangesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FakeTransactionManager : ITransactionManager
    {
        public Task<ITransaction> BeginAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<ITransaction>(new FakeTransaction());
    }

    private sealed class FakeTransaction : ITransaction
    {
        public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task RollbackAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }


    private sealed class WebhookMetricCapture : IDisposable
    {
        private readonly ConcurrentQueue<string> _statuses = new();
        private readonly ConcurrentQueue<string> _transitions = new();
        private readonly MeterListener _listener;
        private int _duplicates;

        public int Duplicates => Volatile.Read(ref _duplicates);

        public IReadOnlyList<string> PaymentStatuses =>
            _statuses.ToArray();

        public IReadOnlyList<string> PaymentTransitions =>
            _transitions.ToArray();

        public WebhookMetricCapture()
        {
            _listener = new MeterListener
            {
                InstrumentPublished = (instrument, listener) =>
                {
                    if (instrument.Meter.Name != BookingMetrics.MeterName)
                    {
                        return;
                    }

                    if (instrument.Name is
                        "bookify.webhooks.duplicates" or
                        "bookify.payments.outcomes" or
                        "bookify.payments.transitions")
                    {
                        listener.EnableMeasurementEvents(instrument);
                    }
                }
            };

            _listener.SetMeasurementEventCallback<long>(
                (instrument, count, tags, _) =>
                {
                    if (instrument.Name == "bookify.webhooks.duplicates")
                    {
                        Interlocked.Add(ref _duplicates, (int)count);
                        return;
                    }

                    foreach (KeyValuePair<string, object?> tag in tags)
                    {
                        if (tag.Key != "payment.status" ||
                            tag.Value is not string status)
                        {
                            continue;
                        }

                        if (instrument.Name == "bookify.payments.outcomes")
                        {
                            _statuses.Enqueue(status);
                        }
                        else if (instrument.Name ==
                                 "bookify.payments.transitions")
                        {
                            _transitions.Enqueue(status);
                        }
                    }
                });

            _listener.Start();
        }

        public void Dispose()
        {
            _listener.Dispose();
        }
    }
}
