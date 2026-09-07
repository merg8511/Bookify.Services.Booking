using Bookify.Services.Booking.Application.Abstractions.Payments;
using Bookify.Services.Booking.Application.Abstractions.Persistence;
using Bookify.Services.Booking.Application.Abstractions.Persistence.Repositories;
using Bookify.Services.Booking.Application.Abstractions.Time;
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

    private static readonly DateTimeOffset UtcNow =
        new(
            2026,
            9,
            6,
            11,
            0,
            0,
            TimeSpan.Zero);

    [Fact]
    public async Task HandleAsync_WhenSignatureIsInvalid_ShouldReturnFailureBeforeOpeningTransaction()
    {
        // Arrange
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

        var unitOfWork =
            new SpyUnitOfWork();

        var transactionManager =
            new SpyTransactionManager();

        ProcessStripeWebhookCommandHandler handler =
            CreateHandler(
                booking,
                payment,
                unitOfWork,
                transactionManager,
                signatureVerifier);

        string payload =
            CreatePayload(
                StripeWebhookEventTypes
                    .PaymentIntentSucceeded,
                booking.Id,
                attempt.ExternalReference);

        // Act
        Result result =
            await handler.HandleAsync(
                new ProcessStripeWebhookCommand(
                    payload,
                    ValidSignatureHeader),
                TestContext.Current.CancellationToken);

        // Assert
        Assert.True(
            result.IsFailure);

        Assert.Equal(
            StripeWebhookSignatureErrors
                .InvalidSignature,
            result.Error);

        Assert.Equal(
            BookingStatus.PendingPayment,
            booking.Status);

        Assert.Equal(
            PaymentStatus.Pending,
            payment.Status);

        Assert.Equal(
            PaymentAttemptStatus.Pending,
            attempt.Status);

        Assert.Equal(
            1,
            signatureVerifier.VerifyCallCount);

        Assert.Equal(
            0,
            transactionManager.BeginCallCount);

        Assert.Equal(
            0,
            unitOfWork.SaveChangesCallCount);
    }

    [Fact]
    public async Task HandleAsync_WhenWebhookSecretIsNotConfigured_ShouldReturnFailureBeforeOpeningTransaction()
    {
        // Arrange
        var signatureVerifier =
            new StubStripeWebhookSignatureVerifier(
                StripeWebhookSignatureErrors
                    .WebhookSecretNotConfigured);

        var transactionManager =
            new SpyTransactionManager();

        ProcessStripeWebhookCommandHandler handler =
            CreateHandler(
                booking: null,
                payment: null,
                unitOfWork:
                    new SpyUnitOfWork(),
                transactionManager,
                signatureVerifier);

        string payload =
            CreatePayload(
                "payment_intent.processing",
                Guid.NewGuid(),
                "pi_test");

        // Act
        Result result =
            await handler.HandleAsync(
                new ProcessStripeWebhookCommand(
                    payload,
                    ValidSignatureHeader),
                TestContext.Current.CancellationToken);

        // Assert
        Assert.True(
            result.IsFailure);

        Assert.Equal(
            StripeWebhookSignatureErrors
                .WebhookSecretNotConfigured,
            result.Error);

        Assert.Equal(
            0,
            transactionManager.BeginCallCount);
    }

    [Fact]
    public async Task HandleAsync_WhenSignatureHeaderIsMissing_ShouldReturnFailureBeforeVerification()
    {
        // Arrange
        var signatureVerifier =
            new StubStripeWebhookSignatureVerifier();

        var transactionManager =
            new SpyTransactionManager();

        ProcessStripeWebhookCommandHandler handler =
            CreateHandler(
                booking: null,
                payment: null,
                unitOfWork:
                    new SpyUnitOfWork(),
                transactionManager,
                signatureVerifier);

        string payload =
            CreatePayload(
                "payment_intent.processing",
                Guid.NewGuid(),
                "pi_test");

        // Act
        Result result =
            await handler.HandleAsync(
                new ProcessStripeWebhookCommand(
                    payload,
                    string.Empty),
                TestContext.Current.CancellationToken);

        // Assert
        Assert.True(
            result.IsFailure);

        Assert.Equal(
            StripeWebhookSignatureErrors
                .SignatureRequired,
            result.Error);

        Assert.Equal(
            0,
            signatureVerifier.VerifyCallCount);

        Assert.Equal(
            0,
            transactionManager.BeginCallCount);
    }

    [Fact]
    public async Task HandleAsync_WhenEventIsUnsupported_ShouldReturnSuccessWithoutChangingState()
    {
        // Arrange
        DomainBooking booking =
            CreatePendingPaymentBooking();

        (
            Payment payment,
            PaymentAttempt attempt) =
                CreatePendingPayment(
                    booking);

        var signatureVerifier =
            new StubStripeWebhookSignatureVerifier();

        var unitOfWork =
            new SpyUnitOfWork();

        var transactionManager =
            new SpyTransactionManager();

        ProcessStripeWebhookCommandHandler handler =
            CreateHandler(
                booking,
                payment,
                unitOfWork,
                transactionManager,
                signatureVerifier);

        string payload =
            CreatePayload(
                "payment_intent.processing",
                booking.Id,
                attempt.ExternalReference);

        // Act
        Result result =
            await handler.HandleAsync(
                new ProcessStripeWebhookCommand(
                    payload,
                    ValidSignatureHeader),
                TestContext.Current.CancellationToken);

        // Assert
        Assert.True(
            result.IsSuccess);

        Assert.Equal(
            BookingStatus.PendingPayment,
            booking.Status);

        Assert.Equal(
            PaymentStatus.Pending,
            payment.Status);

        Assert.Equal(
            PaymentAttemptStatus.Pending,
            attempt.Status);

        Assert.Equal(
            1,
            signatureVerifier.VerifyCallCount);

        Assert.Equal(
            0,
            unitOfWork.SaveChangesCallCount);

        Assert.Equal(
            0,
            transactionManager.BeginCallCount);
    }

    [Fact]
    public async Task HandleAsync_WhenSupportedEventHasNoBookifyMetadata_ShouldReturnSuccessWithoutChangingState()
    {
        // Arrange
        DomainBooking booking =
            CreatePendingPaymentBooking();

        (
            Payment payment,
            PaymentAttempt attempt) =
                CreatePendingPayment(
                    booking);

        var unitOfWork =
            new SpyUnitOfWork();

        var transactionManager =
            new SpyTransactionManager();

        ProcessStripeWebhookCommandHandler handler =
            CreateHandler(
                booking,
                payment,
                unitOfWork,
                transactionManager);

        string payload =
            CreatePayload(
                StripeWebhookEventTypes
                    .PaymentIntentSucceeded,
                bookingId: null,
                externalReference:
                    attempt.ExternalReference);

        // Act
        Result result =
            await handler.HandleAsync(
                new ProcessStripeWebhookCommand(
                    payload,
                    ValidSignatureHeader),
                TestContext.Current.CancellationToken);

        // Assert
        Assert.True(
            result.IsSuccess);

        Assert.Equal(
            BookingStatus.PendingPayment,
            booking.Status);

        Assert.Equal(
            PaymentStatus.Pending,
            payment.Status);

        Assert.Equal(
            0,
            unitOfWork.SaveChangesCallCount);

        Assert.Equal(
            0,
            transactionManager.BeginCallCount);
    }

    [Fact]
    public async Task HandleAsync_WhenPaymentIntentSucceeded_ShouldReconcileBookingAndPayment()
    {
        // Arrange
        DomainBooking booking =
            CreatePendingPaymentBooking();

        (
            Payment payment,
            PaymentAttempt attempt) =
                CreatePendingPayment(
                    booking);

        var unitOfWork =
            new SpyUnitOfWork();

        var transactionManager =
            new SpyTransactionManager();

        ProcessStripeWebhookCommandHandler handler =
            CreateHandler(
                booking,
                payment,
                unitOfWork,
                transactionManager);

        string payload =
            CreatePayload(
                StripeWebhookEventTypes
                    .PaymentIntentSucceeded,
                booking.Id,
                attempt.ExternalReference);

        // Act
        Result result =
            await handler.HandleAsync(
                new ProcessStripeWebhookCommand(
                    payload,
                    ValidSignatureHeader),
                TestContext.Current.CancellationToken);

        // Assert
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
            UtcNow,
            payment.CompletedAtUtc);

        Assert.Equal(
            UtcNow,
            attempt.CompletedAtUtc);

        Assert.Equal(
            1,
            unitOfWork.SaveChangesCallCount);

        Assert.Equal(
            1,
            transactionManager.BeginCallCount);

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
    public async Task HandleAsync_WhenPaymentIntentBecomesTerminalWithoutSuccess_ShouldKeepBookingPendingPayment(
        string eventType,
        PaymentStatus expectedPaymentStatus,
        PaymentAttemptStatus expectedAttemptStatus)
    {
        // Arrange
        DomainBooking booking =
            CreatePendingPaymentBooking();

        (
            Payment payment,
            PaymentAttempt attempt) =
                CreatePendingPayment(
                    booking);

        var unitOfWork =
            new SpyUnitOfWork();

        var transactionManager =
            new SpyTransactionManager();

        ProcessStripeWebhookCommandHandler handler =
            CreateHandler(
                booking,
                payment,
                unitOfWork,
                transactionManager);

        string payload =
            CreatePayload(
                eventType,
                booking.Id,
                attempt.ExternalReference);

        // Act
        Result result =
            await handler.HandleAsync(
                new ProcessStripeWebhookCommand(
                    payload,
                    ValidSignatureHeader),
                TestContext.Current.CancellationToken);

        // Assert
        Assert.True(
            result.IsSuccess);

        Assert.Equal(
            BookingStatus.PendingPayment,
            booking.Status);

        Assert.Equal(
            expectedPaymentStatus,
            payment.Status);

        Assert.Equal(
            expectedAttemptStatus,
            attempt.Status);

        Assert.Equal(
            1,
            unitOfWork.SaveChangesCallCount);

        Assert.Equal(
            1,
            transactionManager.Transaction.CommitCallCount);
    }

    [Fact]
    public async Task HandleAsync_WhenPayloadIsMalformed_ShouldReturnValidationFailureWithoutTransaction()
    {
        // Arrange
        var unitOfWork =
            new SpyUnitOfWork();

        var transactionManager =
            new SpyTransactionManager();

        ProcessStripeWebhookCommandHandler handler =
            CreateHandler(
                booking: null,
                payment: null,
                unitOfWork,
                transactionManager);

        // Act
        Result result =
            await handler.HandleAsync(
                new ProcessStripeWebhookCommand(
                    "{ invalid-json",
                    ValidSignatureHeader),
                TestContext.Current.CancellationToken);

        // Assert
        Assert.True(
            result.IsFailure);

        Assert.Equal(
            StripeWebhookErrors.InvalidPayload,
            result.Error);

        Assert.Equal(
            0,
            unitOfWork.SaveChangesCallCount);

        Assert.Equal(
            0,
            transactionManager.BeginCallCount);
    }

    [Fact]
    public async Task HandleAsync_WhenPaymentAttemptCannotBeCorrelated_ShouldReturnFailureAndRollback()
    {
        // Arrange
        DomainBooking booking =
            CreatePendingPaymentBooking();

        (
            Payment payment,
            PaymentAttempt attempt) =
                CreatePendingPayment(
                    booking);

        var unitOfWork =
            new SpyUnitOfWork();

        var transactionManager =
            new SpyTransactionManager();

        ProcessStripeWebhookCommandHandler handler =
            CreateHandler(
                booking,
                payment,
                unitOfWork,
                transactionManager);

        const string unknownExternalReference =
            "pi_unknown";

        string payload =
            CreatePayload(
                StripeWebhookEventTypes
                    .PaymentIntentSucceeded,
                booking.Id,
                unknownExternalReference);

        // Act
        Result result =
            await handler.HandleAsync(
                new ProcessStripeWebhookCommand(
                    payload,
                    ValidSignatureHeader),
                TestContext.Current.CancellationToken);

        // Assert
        Assert.True(
            result.IsFailure);

        Assert.Equal(
            StripeWebhookErrors
                .PaymentAttemptNotFound(
                    payment.Id,
                    unknownExternalReference),
            result.Error);

        Assert.Equal(
            BookingStatus.PendingPayment,
            booking.Status);

        Assert.Equal(
            PaymentStatus.Pending,
            payment.Status);

        Assert.Equal(
            PaymentAttemptStatus.Pending,
            attempt.Status);

        Assert.Equal(
            0,
            unitOfWork.SaveChangesCallCount);

        Assert.Equal(
            1,
            transactionManager.Transaction.RollbackCallCount);
    }

    private static ProcessStripeWebhookCommandHandler
        CreateHandler(
            DomainBooking? booking,
            Payment? payment,
            IUnitOfWork unitOfWork,
            ITransactionManager transactionManager,
            IStripeWebhookSignatureVerifier? signatureVerifier = null)
    {
        return new ProcessStripeWebhookCommandHandler(
            new StubBookingRepository(
                booking),
            new StubPaymentRepository(
                payment),
            signatureVerifier ??
                new StubStripeWebhookSignatureVerifier(),
            unitOfWork,
            transactionManager,
            new StubClock(
                UtcNow));
    }

    private static string CreatePayload(
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
                    $"evt_{Guid.NewGuid():N}",

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
                .Value)
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

        public int VerifyCallCount
        {
            get;
            private set;
        }

        public Result Verify(
            string rawBody,
            string signatureHeader,
            DateTimeOffset utcNow)
        {
            VerifyCallCount++;

            return _result;
        }
    }

    private sealed class StubBookingRepository
        : IBookingRepository
    {
        private readonly DomainBooking? _booking;

        public StubBookingRepository(
            DomainBooking? booking)
        {
            _booking =
                booking;
        }

        public Task<DomainBooking?> GetByIdAsync(
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
        private readonly Payment? _payment;

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
}
