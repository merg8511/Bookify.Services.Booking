using Bookify.Services.Booking.Application.Abstractions.Payments;
using Bookify.Services.Booking.Application.Abstractions.Payments.Webhooks;
using Bookify.Services.Booking.Application.Abstractions.Persistence;
using Bookify.Services.Booking.Application.Abstractions.Persistence.Repositories;
using Bookify.Services.Booking.Application.Abstractions.Time;
using Bookify.Services.Booking.Application.Payments.Initiate;
using Bookify.Services.Booking.Application.Payments.Webhooks;
using Bookify.Services.Booking.Application.Payments.Webhooks.Stripe;
using Bookify.Services.Booking.Domain.Bookings;
using Bookify.Services.Booking.Domain.Bookings.ValueObjects;
using Bookify.Services.Booking.Domain.Payments;
using Bookify.Services.Booking.Domain.Properties;
using Bookify.Services.Booking.Domain.Shared;
using Bookify.Services.Booking.Domain.Shared.ValueObjects;
using System.Text.Json;

using DomainBooking =
    Bookify.Services.Booking.Domain.Bookings.Booking;

namespace Bookify.Services.Booking.Application.Tests.Payments.Webhooks.Stripe;

public sealed class ProcessStripeWebhookCommandHandlerTests
{
    private const string ValidSignatureHeader =
        "t=1,v1=test";

    private const string EventId =
        "evt_bookify_test";

    private static readonly DateTimeOffset UtcNow =
        new(
            2026,
            9,
            6,
            19,
            0,
            0,
            TimeSpan.Zero);

    [Fact]
    public async Task HandleAsync_WhenSignatureIsInvalid_ShouldReturnFailureBeforePersistence()
    {
        DomainBooking booking =
            CreatePendingPaymentBooking();

        (
            Payment payment,
            PaymentAttempt attempt) =
                CreatePendingPayment(
                    booking);

        var signatureVerifier =
            new StubStripeWebhookSignatureVerifier(
                StripeWebhookSignatureErrors
                    .InvalidSignature);

        var eventStore =
            new SpyPaymentWebhookEventStore();

        var unitOfWork =
            new SpyUnitOfWork();

        var transactionManager =
            new SpyTransactionManager();

        ProcessStripeWebhookCommandHandler handler =
            CreateHandler(
                booking,
                payment,
                eventStore,
                unitOfWork,
                transactionManager,
                signatureVerifier);

        string payload =
            CreatePayload(
                EventId,
                StripeWebhookEventTypes
                    .PaymentIntentSucceeded,
                booking.Id,
                attempt.ExternalReference);

        Result result =
            await handler.HandleAsync(
                new ProcessStripeWebhookCommand(
                    payload,
                    ValidSignatureHeader),
                TestContext.Current.CancellationToken);

        Assert.True(
            result.IsFailure);

        Assert.Equal(
            StripeWebhookSignatureErrors
                .InvalidSignature,
            result.Error);

        Assert.Equal(
            0,
            eventStore.PrepareCallCount);

        Assert.Equal(
            0,
            transactionManager.BeginCallCount);

        Assert.Equal(
            0,
            unitOfWork.SaveChangesCallCount);

        Assert.Equal(
            PaymentStatus.Pending,
            payment.Status);

        Assert.Equal(
            BookingStatus.PendingPayment,
            booking.Status);
    }

    [Fact]
    public async Task HandleAsync_WhenPayloadIsMalformed_ShouldNotPersistWebhookEvent()
    {
        var eventStore =
            new SpyPaymentWebhookEventStore();

        var transactionManager =
            new SpyTransactionManager();

        ProcessStripeWebhookCommandHandler handler =
            CreateHandler(
                booking: null,
                payment: null,
                eventStore,
                new SpyUnitOfWork(),
                transactionManager);

        Result result =
            await handler.HandleAsync(
                new ProcessStripeWebhookCommand(
                    "{ invalid-json",
                    ValidSignatureHeader),
                TestContext.Current.CancellationToken);

        Assert.True(
            result.IsFailure);

        Assert.Equal(
            StripeWebhookErrors.InvalidPayload,
            result.Error);

        Assert.Equal(
            0,
            eventStore.PrepareCallCount);

        Assert.Equal(
            0,
            transactionManager.BeginCallCount);
    }

    [Fact]
    public async Task HandleAsync_WhenEventWasAlreadyProcessed_ShouldReturnSuccessWithoutExecutingTransitionAgain()
    {
        DomainBooking booking =
            CreatePendingPaymentBooking();

        (
            Payment payment,
            PaymentAttempt attempt) =
                CreatePendingPayment(
                    booking);

        var eventStore =
            new SpyPaymentWebhookEventStore(
                PaymentWebhookPreparationStatus
                    .AlreadyProcessed);

        var unitOfWork =
            new SpyUnitOfWork();

        var transactionManager =
            new SpyTransactionManager();

        ProcessStripeWebhookCommandHandler handler =
            CreateHandler(
                booking,
                payment,
                eventStore,
                unitOfWork,
                transactionManager);

        string payload =
            CreatePayload(
                EventId,
                StripeWebhookEventTypes
                    .PaymentIntentSucceeded,
                booking.Id,
                attempt.ExternalReference);

        Result result =
            await handler.HandleAsync(
                new ProcessStripeWebhookCommand(
                    payload,
                    ValidSignatureHeader),
                TestContext.Current.CancellationToken);

        Assert.True(
            result.IsSuccess);

        Assert.Equal(
            1,
            eventStore.PrepareCallCount);

        Assert.Equal(
            0,
            eventStore.MarkProcessedCallCount);

        Assert.Equal(
            0,
            eventStore.MarkFailedCallCount);

        Assert.Equal(
            0,
            unitOfWork.SaveChangesCallCount);

        Assert.Equal(
            PaymentStatus.Pending,
            payment.Status);

        Assert.Equal(
            PaymentAttemptStatus.Pending,
            attempt.Status);

        Assert.Equal(
            BookingStatus.PendingPayment,
            booking.Status);

        Assert.Equal(
            1,
            transactionManager.Transaction.CommitCallCount);
    }

    [Fact]
    public async Task HandleAsync_WhenEventIsUnsupported_ShouldPersistEventAsProcessedWithoutChangingPayment()
    {
        DomainBooking booking =
            CreatePendingPaymentBooking();

        (
            Payment payment,
            PaymentAttempt attempt) =
                CreatePendingPayment(
                    booking);

        var eventStore =
            new SpyPaymentWebhookEventStore();

        var unitOfWork =
            new SpyUnitOfWork();

        var transactionManager =
            new SpyTransactionManager();

        ProcessStripeWebhookCommandHandler handler =
            CreateHandler(
                booking,
                payment,
                eventStore,
                unitOfWork,
                transactionManager);

        string payload =
            CreatePayload(
                EventId,
                "payment_intent.processing",
                booking.Id,
                attempt.ExternalReference);

        Result result =
            await handler.HandleAsync(
                new ProcessStripeWebhookCommand(
                    payload,
                    ValidSignatureHeader),
                TestContext.Current.CancellationToken);

        Assert.True(
            result.IsSuccess);

        Assert.Equal(
            1,
            eventStore.PrepareCallCount);

        Assert.Equal(
            1,
            eventStore.MarkProcessedCallCount);

        Assert.Equal(
            0,
            eventStore.MarkFailedCallCount);

        Assert.Equal(
            1,
            unitOfWork.SaveChangesCallCount);

        Assert.Equal(
            PaymentStatus.Pending,
            payment.Status);

        Assert.Equal(
            PaymentAttemptStatus.Pending,
            attempt.Status);

        Assert.Equal(
            BookingStatus.PendingPayment,
            booking.Status);
    }

    [Fact]
    public async Task HandleAsync_WhenSupportedEventHasNoBookifyMetadata_ShouldPersistEventAsProcessedWithoutChangingPayment()
    {
        DomainBooking booking =
            CreatePendingPaymentBooking();

        (
            Payment payment,
            PaymentAttempt attempt) =
                CreatePendingPayment(
                    booking);

        var eventStore =
            new SpyPaymentWebhookEventStore();

        var unitOfWork =
            new SpyUnitOfWork();

        ProcessStripeWebhookCommandHandler handler =
            CreateHandler(
                booking,
                payment,
                eventStore,
                unitOfWork,
                new SpyTransactionManager());

        string payload =
            CreatePayload(
                EventId,
                StripeWebhookEventTypes
                    .PaymentIntentSucceeded,
                bookingId: null,
                attempt.ExternalReference);

        Result result =
            await handler.HandleAsync(
                new ProcessStripeWebhookCommand(
                    payload,
                    ValidSignatureHeader),
                TestContext.Current.CancellationToken);

        Assert.True(
            result.IsSuccess);

        Assert.Equal(
            1,
            eventStore.MarkProcessedCallCount);

        Assert.Equal(
            1,
            unitOfWork.SaveChangesCallCount);

        Assert.Equal(
            BookingStatus.PendingPayment,
            booking.Status);

        Assert.Equal(
            PaymentStatus.Pending,
            payment.Status);
    }

    [Fact]
    public async Task HandleAsync_WhenPaymentIntentSucceeded_ShouldReconcileAndMarkEventProcessed()
    {
        DomainBooking booking =
            CreatePendingPaymentBooking();

        (
            Payment payment,
            PaymentAttempt attempt) =
                CreatePendingPayment(
                    booking);

        var eventStore =
            new SpyPaymentWebhookEventStore();

        var unitOfWork =
            new SpyUnitOfWork();

        var transactionManager =
            new SpyTransactionManager();

        ProcessStripeWebhookCommandHandler handler =
            CreateHandler(
                booking,
                payment,
                eventStore,
                unitOfWork,
                transactionManager);

        string payload =
            CreatePayload(
                EventId,
                StripeWebhookEventTypes
                    .PaymentIntentSucceeded,
                booking.Id,
                attempt.ExternalReference);

        Result result =
            await handler.HandleAsync(
                new ProcessStripeWebhookCommand(
                    payload,
                    ValidSignatureHeader),
                TestContext.Current.CancellationToken);

        Assert.True(
            result.IsSuccess);

        Assert.Equal(
            BookingStatus.Paid,
            booking.Status);

        Assert.Equal(
            PaymentStatus.Succeeded,
            payment.Status);

        Assert.Equal(
            PaymentAttemptStatus.Succeeded,
            attempt.Status);

        Assert.Equal(
            1,
            eventStore.MarkProcessedCallCount);

        Assert.Equal(
            0,
            eventStore.MarkFailedCallCount);

        Assert.Equal(
            1,
            unitOfWork.SaveChangesCallCount);

        Assert.Equal(
            1,
            transactionManager.Transaction.CommitCallCount);

        Assert.Equal(
            0,
            transactionManager.Transaction.RollbackCallCount);
    }

    [Theory]
    [InlineData(
        StripeWebhookEventTypes.PaymentIntentPaymentFailed,
        PaymentStatus.Failed,
        PaymentAttemptStatus.Failed)]
    [InlineData(
        StripeWebhookEventTypes.PaymentIntentCanceled,
        PaymentStatus.Cancelled,
        PaymentAttemptStatus.Cancelled)]
    public async Task HandleAsync_WhenPaymentIntentBecomesTerminalWithoutSuccess_ShouldMarkEventProcessed(
        string eventType,
        PaymentStatus expectedPaymentStatus,
        PaymentAttemptStatus expectedAttemptStatus)
    {
        DomainBooking booking =
            CreatePendingPaymentBooking();

        (
            Payment payment,
            PaymentAttempt attempt) =
                CreatePendingPayment(
                    booking);

        var eventStore =
            new SpyPaymentWebhookEventStore();

        ProcessStripeWebhookCommandHandler handler =
            CreateHandler(
                booking,
                payment,
                eventStore,
                new SpyUnitOfWork(),
                new SpyTransactionManager());

        string payload =
            CreatePayload(
                EventId,
                eventType,
                booking.Id,
                attempt.ExternalReference);

        Result result =
            await handler.HandleAsync(
                new ProcessStripeWebhookCommand(
                    payload,
                    ValidSignatureHeader),
                TestContext.Current.CancellationToken);

        Assert.True(
            result.IsSuccess);

        Assert.Equal(
            expectedPaymentStatus,
            payment.Status);

        Assert.Equal(
            expectedAttemptStatus,
            attempt.Status);

        Assert.Equal(
            BookingStatus.PendingPayment,
            booking.Status);

        Assert.Equal(
            1,
            eventStore.MarkProcessedCallCount);

        Assert.Equal(
            0,
            eventStore.MarkFailedCallCount);
    }

    [Fact]
    public async Task HandleAsync_WhenPaymentAttemptCannotBeCorrelated_ShouldPersistEventAsFailed()
    {
        DomainBooking booking =
            CreatePendingPaymentBooking();

        (
            Payment payment,
            PaymentAttempt attempt) =
                CreatePendingPayment(
                    booking);

        var eventStore =
            new SpyPaymentWebhookEventStore();

        var unitOfWork =
            new SpyUnitOfWork();

        var transactionManager =
            new SpyTransactionManager();

        ProcessStripeWebhookCommandHandler handler =
            CreateHandler(
                booking,
                payment,
                eventStore,
                unitOfWork,
                transactionManager);

        const string unknownExternalReference =
            "pi_unknown";

        string payload =
            CreatePayload(
                EventId,
                StripeWebhookEventTypes
                    .PaymentIntentSucceeded,
                booking.Id,
                unknownExternalReference);

        Result result =
            await handler.HandleAsync(
                new ProcessStripeWebhookCommand(
                    payload,
                    ValidSignatureHeader),
                TestContext.Current.CancellationToken);

        Error expectedError =
            StripeWebhookErrors
                .PaymentAttemptNotFound(
                    payment.Id,
                    unknownExternalReference);

        Assert.True(
            result.IsFailure);

        Assert.Equal(
            expectedError,
            result.Error);

        Assert.Equal(
            1,
            eventStore.MarkFailedCallCount);

        Assert.Equal(
            expectedError,
            eventStore.LastFailure);

        Assert.Equal(
            0,
            eventStore.MarkProcessedCallCount);

        Assert.Equal(
            1,
            unitOfWork.SaveChangesCallCount);

        Assert.Equal(
            1,
            transactionManager.Transaction.CommitCallCount);

        Assert.Equal(
            0,
            transactionManager.Transaction.RollbackCallCount);

        Assert.Equal(
            BookingStatus.PendingPayment,
            booking.Status);

        Assert.Equal(
            PaymentStatus.Pending,
            payment.Status);

        Assert.Equal(
            PaymentAttemptStatus.Pending,
            attempt.Status);
    }

    [Fact]
    public async Task HandleAsync_WhenEventPreparationFails_ShouldRollbackWithoutSaving()
    {
        DomainBooking booking =
            CreatePendingPaymentBooking();

        (
            Payment payment,
            PaymentAttempt attempt) =
                CreatePendingPayment(
                    booking);

        Error preparationError =
            PaymentWebhookPersistenceErrors
                .EventIdentityMismatch(
                    EventId);

        var eventStore =
            new SpyPaymentWebhookEventStore(
                preparationError);

        var unitOfWork =
            new SpyUnitOfWork();

        var transactionManager =
            new SpyTransactionManager();

        ProcessStripeWebhookCommandHandler handler =
            CreateHandler(
                booking,
                payment,
                eventStore,
                unitOfWork,
                transactionManager);

        string payload =
            CreatePayload(
                EventId,
                StripeWebhookEventTypes
                    .PaymentIntentSucceeded,
                booking.Id,
                attempt.ExternalReference);

        Result result =
            await handler.HandleAsync(
                new ProcessStripeWebhookCommand(
                    payload,
                    ValidSignatureHeader),
                TestContext.Current.CancellationToken);

        Assert.True(
            result.IsFailure);

        Assert.Equal(
            preparationError,
            result.Error);

        Assert.Equal(
            0,
            unitOfWork.SaveChangesCallCount);

        Assert.Equal(
            1,
            transactionManager.Transaction.RollbackCallCount);

        Assert.Equal(
            0,
            transactionManager.Transaction.CommitCallCount);
    }

    private static ProcessStripeWebhookCommandHandler
        CreateHandler(
            DomainBooking? booking,
            Payment? payment,
            IPaymentWebhookEventStore eventStore,
            IUnitOfWork unitOfWork,
            ITransactionManager transactionManager,
            IStripeWebhookSignatureVerifier?
                signatureVerifier = null)
    {
        return new ProcessStripeWebhookCommandHandler(
            new StubBookingRepository(
                booking),
            new StubPaymentRepository(
                payment),
            signatureVerifier ??
                new StubStripeWebhookSignatureVerifier(),
            eventStore,
            new StubPaymentInitiationLock(),
            unitOfWork,
            transactionManager,
            new StubClock(
                UtcNow));
    }

    private static string CreatePayload(
        string eventId,
        string eventType,
        Guid? bookingId,
        string externalReference)
    {
        var metadata =
            new Dictionary<string, string>();

        if (bookingId.HasValue)
        {
            metadata[
                "bookify_booking_id"] =
                    bookingId.Value
                        .ToString("D");
        }

        return JsonSerializer.Serialize(
            new
            {
                id =
                    eventId,

                type =
                    eventType,

                data =
                    new
                    {
                        @object =
                            new
                            {
                                id =
                                    externalReference,

                                metadata
                            }
                    }
            });
    }

    private static DomainBooking
        CreatePendingPaymentBooking()
    {
        RentableUnit rentableUnit =
            RentableUnit.Create(
                Guid.NewGuid(),
                "Room A",
                RentableUnitType.Room,
                maximumCapacity: 4,
                maxBaseGuests: 2)
            .Value;

        StayPeriod stayPeriod =
            StayPeriod.Create(
                new DateOnly(
                    2026,
                    9,
                    10),
                new DateOnly(
                    2026,
                    9,
                    12))
            .Value;

        DomainBooking booking =
            DomainBooking.Create(
                rentableUnit,
                stayPeriod,
                GuestCount.Create(
                    2)
                .Value, GuestDetails.Create("John Doe", "john@example.com", "+50377778888").Value)
            .Value;

        Assert.True(
            booking.Approve().IsSuccess);

        return booking;
    }

    private static (
        Payment Payment,
        PaymentAttempt Attempt)
        CreatePendingPayment(
            DomainBooking booking)
    {
        Payment payment =
            Payment.Create(
                booking.Id,
                Money.Create(
                    200m,
                    "USD")
                .Value,
                UtcNow.AddMinutes(-2))
            .Value;

        Result<PaymentAttempt> attemptResult =
            payment.AddAttempt(
                $"operation-{Guid.NewGuid():N}",
                $"pi_{Guid.NewGuid():N}",
                UtcNow.AddMinutes(-1));

        Assert.True(
            attemptResult.IsSuccess);

        return (
            payment,
            attemptResult.Value);
    }

    private sealed class
        StubStripeWebhookSignatureVerifier
        : IStripeWebhookSignatureVerifier
    {
        private readonly Result _result;

        public StubStripeWebhookSignatureVerifier()
        {
            _result =
                Result.Success();
        }

        public StubStripeWebhookSignatureVerifier(
            Error error)
        {
            _result =
                Result.Failure(
                    error);
        }

        public Result Verify(
            string rawBody,
            string signatureHeader,
            DateTimeOffset utcNow)
        {
            return _result;
        }
    }

    private sealed class
        SpyPaymentWebhookEventStore
        : IPaymentWebhookEventStore
    {
        private static readonly Guid EventRecordId =
            Guid.NewGuid();

        private readonly
            PaymentWebhookPreparationStatus
            _preparationStatus;

        private readonly Error?
            _preparationError;

        public SpyPaymentWebhookEventStore(
            PaymentWebhookPreparationStatus
                preparationStatus =
                    PaymentWebhookPreparationStatus
                        .ReadyToProcess)
        {
            _preparationStatus =
                preparationStatus;
        }

        public SpyPaymentWebhookEventStore(
            Error preparationError)
        {
            _preparationStatus =
                PaymentWebhookPreparationStatus
                    .ReadyToProcess;

            _preparationError =
                preparationError;
        }

        public int PrepareCallCount
        {
            get;
            private set;
        }

        public int MarkProcessedCallCount
        {
            get;
            private set;
        }

        public int MarkFailedCallCount
        {
            get;
            private set;
        }

        public Error? LastFailure
        {
            get;
            private set;
        }

        public Task<
            Result<PaymentWebhookEventPreparation>>
            PrepareAsync(
                string provider,
                string eventId,
                string eventType,
                DateTimeOffset receivedAtUtc,
                CancellationToken cancellationToken = default)
        {
            cancellationToken
                .ThrowIfCancellationRequested();

            PrepareCallCount++;

            if (_preparationError is not null)
            {
                return Task.FromResult(
                    Result<
                        PaymentWebhookEventPreparation>
                        .Failure(
                            _preparationError));
            }

            return Task.FromResult(
                Result<
                    PaymentWebhookEventPreparation>
                    .Success(
                        new PaymentWebhookEventPreparation(
                            EventRecordId,
                            _preparationStatus)));
        }

        public Task MarkProcessedAsync(
            Guid eventRecordId,
            DateTimeOffset processedAtUtc,
            CancellationToken cancellationToken = default)
        {
            cancellationToken
                .ThrowIfCancellationRequested();

            MarkProcessedCallCount++;

            return Task.CompletedTask;
        }

        public Task MarkFailedAsync(
            Guid eventRecordId,
            Error error,
            CancellationToken cancellationToken = default)
        {
            cancellationToken
                .ThrowIfCancellationRequested();

            MarkFailedCallCount++;

            LastFailure =
                error;

            return Task.CompletedTask;
        }

        public Task<StoredPaymentWebhookEvent?>
            GetAsync(
                string provider,
                string eventId,
                CancellationToken cancellationToken = default)
        {
            cancellationToken
                .ThrowIfCancellationRequested();

            return Task.FromResult<
                StoredPaymentWebhookEvent?>(
                    null);
        }
    }

    private sealed class StubBookingRepository
        : IBookingRepository
    {
        private readonly DomainBooking?
            _booking;

        public StubBookingRepository(
            DomainBooking? booking)
        {
            _booking =
                booking;
        }

        public Task<DomainBooking?>
            GetByIdAsync(
                Guid bookingId,
                CancellationToken cancellationToken = default)
        {
            cancellationToken
                .ThrowIfCancellationRequested();

            return Task.FromResult(
                _booking?.Id ==
                bookingId
                    ? _booking
                    : null);
        }

        public void Add(
            DomainBooking booking)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class StubPaymentRepository
        : IPaymentRepository
    {
        private readonly Payment?
            _payment;

        public StubPaymentRepository(
            Payment? payment)
        {
            _payment =
                payment;
        }

        public Task<Payment?>
            GetByBookingIdAsync(
                Guid bookingId,
                CancellationToken cancellationToken = default)
        {
            cancellationToken
                .ThrowIfCancellationRequested();

            return Task.FromResult(
                _payment?.BookingId ==
                bookingId
                    ? _payment
                    : null);
        }

        public void Add(
            Payment payment)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class SpyUnitOfWork
        : IUnitOfWork
    {
        public int SaveChangesCallCount
        {
            get;
            private set;
        }

        public Task SaveChangesAsync(
            CancellationToken cancellationToken = default)
        {
            cancellationToken
                .ThrowIfCancellationRequested();

            SaveChangesCallCount++;

            return Task.CompletedTask;
        }
    }

    private sealed class StubClock
        : IClock
    {
        public StubClock(
            DateTimeOffset utcNow)
        {
            UtcNow =
                utcNow;
        }

        public DateTimeOffset UtcNow
        {
            get;
        }
    }

    private sealed class SpyTransactionManager
        : ITransactionManager
    {
        public int BeginCallCount
        {
            get;
            private set;
        }

        public SpyTransaction Transaction
        {
            get;
        } = new();

        public Task<ITransaction> BeginAsync(
            CancellationToken cancellationToken = default)
        {
            cancellationToken
                .ThrowIfCancellationRequested();

            BeginCallCount++;

            return Task.FromResult<ITransaction>(
                Transaction);
        }
    }

    private sealed class SpyTransaction
        : ITransaction
    {
        public int CommitCallCount
        {
            get;
            private set;
        }

        public int RollbackCallCount
        {
            get;
            private set;
        }

        public Task CommitAsync(
            CancellationToken cancellationToken = default)
        {
            cancellationToken
                .ThrowIfCancellationRequested();

            CommitCallCount++;

            return Task.CompletedTask;
        }

        public Task RollbackAsync(
            CancellationToken cancellationToken = default)
        {
            cancellationToken
                .ThrowIfCancellationRequested();

            RollbackCallCount++;

            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            return ValueTask.CompletedTask;
        }
    }

    private sealed class StubPaymentInitiationLock
    : IPaymentInitiationLock
    {
        public Task<bool> TryAcquireAsync(
            Guid bookingId,
            CancellationToken cancellationToken = default)
        {
            cancellationToken
                .ThrowIfCancellationRequested();

            return Task.FromResult(
                true);
        }
    }
}
