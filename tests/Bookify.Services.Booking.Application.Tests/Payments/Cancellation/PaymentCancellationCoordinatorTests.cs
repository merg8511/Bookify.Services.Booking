using Bookify.Services.Booking.Application.Abstractions.Payments;
using Bookify.Services.Booking.Application.Abstractions.Time;
using Bookify.Services.Booking.Application.Payments.Cancellation;
using Bookify.Services.Booking.Domain.Bookings;
using Bookify.Services.Booking.Domain.Bookings.ValueObjects;
using Bookify.Services.Booking.Domain.Payments;
using Bookify.Services.Booking.Domain.Properties;
using Bookify.Services.Booking.Domain.Shared;
using Bookify.Services.Booking.Domain.Shared.ValueObjects;

using DomainBooking =
    Bookify.Services.Booking.Domain.Bookings.Booking;

namespace Bookify.Services.Booking.Application.Tests.Payments.Cancellation;

public sealed class PaymentCancellationCoordinatorTests
{
    private static readonly DateTimeOffset UtcNow =
        new(
            2026,
            9,
            4,
            21,
            0,
            0,
            TimeSpan.Zero);

    [Theory]
    [InlineData(PaymentGatewayStatus.Failed)]
    [InlineData(PaymentGatewayStatus.Cancelled)]
    public async Task EnsurePaymentCannotSucceedAsync_WhenPaymentIsAlreadyTerminalWithoutPendingAttempt_ShouldReturnSafe(
        PaymentGatewayStatus terminalStatus)
    {
        // Arrange
        DomainBooking booking =
            CreatePendingPaymentBooking();

        (Payment payment, PaymentAttempt attempt) =
            CreatePendingPayment(
                booking,
                "terminal-external");

        Result transitionResult =
            terminalStatus switch
            {
                PaymentGatewayStatus.Failed =>
                    payment.MarkAttemptAsFailed(
                        attempt.ExternalReference,
                        UtcNow),

                PaymentGatewayStatus.Cancelled =>
                    payment.CancelAttempt(
                        attempt.ExternalReference,
                        UtcNow),

                _ =>
                    throw new InvalidOperationException()
            };

        Assert.True(
            transitionResult.IsSuccess);

        var gateway =
            new SpyPaymentGateway();

        var coordinator =
            CreateCoordinator(
                gateway);

        // Act
        Result<PaymentCancellationOutcome> result =
            await coordinator
                .EnsurePaymentCannotSucceedAsync(
                    payment,
                    booking,
                    TestContext.Current.CancellationToken);

        // Assert
        Assert.True(
            result.IsSuccess);

        Assert.Equal(
            PaymentCancellationOutcome.SafeToCancelBooking,
            result.Value);

        Assert.Equal(
            BookingStatus.PendingPayment,
            booking.Status);

        Assert.Equal(
            0,
            gateway.GetCallCount);

        Assert.Equal(
            0,
            gateway.CancelCallCount);
    }

    [Fact]
    public async Task EnsurePaymentCannotSucceedAsync_WhenPaymentIsPendingWithoutActiveAttempt_ShouldReturnStateUncertain()
    {
        // Arrange
        DomainBooking booking =
            CreatePendingPaymentBooking();

        Payment payment =
            CreatePayment(
                booking);

        var gateway =
            new SpyPaymentGateway();

        var coordinator =
            CreateCoordinator(
                gateway);

        // Act
        Result<PaymentCancellationOutcome> result =
            await coordinator
                .EnsurePaymentCannotSucceedAsync(
                    payment,
                    booking,
                    TestContext.Current.CancellationToken);

        // Assert
        Assert.True(
            result.IsFailure);

        Assert.Equal(
            PaymentCancellationErrors
                .PaymentStateUncertain(
                    payment.Id),
            result.Error);

        Assert.Equal(
            BookingStatus.PendingPayment,
            booking.Status);

        Assert.Equal(
            0,
            gateway.GetCallCount);

        Assert.Equal(
            0,
            gateway.CancelCallCount);
    }

    [Fact]
    public async Task EnsurePaymentCannotSucceedAsync_WhenPersistedPaymentAlreadySucceeded_ShouldReconcileBookingAsPaid()
    {
        // Arrange
        DomainBooking booking =
            CreatePendingPaymentBooking();

        (Payment payment, PaymentAttempt attempt) =
            CreatePendingPayment(
                booking,
                "already-succeeded");

        Result succeedResult =
            payment.MarkAttemptAsSucceeded(
                attempt.ExternalReference,
                UtcNow);

        Assert.True(
            succeedResult.IsSuccess);

        var gateway =
            new SpyPaymentGateway();

        var coordinator =
            CreateCoordinator(
                gateway);

        // Act
        Result<PaymentCancellationOutcome> result =
            await coordinator
                .EnsurePaymentCannotSucceedAsync(
                    payment,
                    booking,
                    TestContext.Current.CancellationToken);

        // Assert
        Assert.True(
            result.IsSuccess);

        Assert.Equal(
            PaymentCancellationOutcome.PaymentSucceeded,
            result.Value);

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
            0,
            gateway.GetCallCount);

        Assert.Equal(
            0,
            gateway.CancelCallCount);
    }

    [Fact]
    public async Task EnsurePaymentCannotSucceedAsync_WhenProviderStatusQueryFails_ShouldReturnFailure()
    {
        // Arrange
        DomainBooking booking =
            CreatePendingPaymentBooking();

        (Payment payment, PaymentAttempt attempt) =
            CreatePendingPayment(
                booking,
                "query-failure");

        var gateway =
            new SpyPaymentGateway(
                getResultFactory:
                    _ =>
                        Result<PaymentGatewayResponse>
                            .Failure(
                                PaymentGatewayErrors.ProviderTimeout));

        var coordinator =
            CreateCoordinator(
                gateway);

        // Act
        Result<PaymentCancellationOutcome> result =
            await coordinator
                .EnsurePaymentCannotSucceedAsync(
                    payment,
                    booking,
                    TestContext.Current.CancellationToken);

        // Assert
        Assert.True(
            result.IsFailure);

        Assert.Equal(
            PaymentGatewayErrors.ProviderTimeout,
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
            gateway.GetCallCount);

        Assert.Equal(
            0,
            gateway.CancelCallCount);
    }

    [Fact]
    public async Task EnsurePaymentCannotSucceedAsync_WhenProviderAlreadySucceeded_ShouldReconcileAndReturnPaymentSucceeded()
    {
        // Arrange
        DomainBooking booking =
            CreatePendingPaymentBooking();

        (Payment payment, PaymentAttempt attempt) =
            CreatePendingPayment(
                booking,
                "provider-succeeded");

        var gateway =
            new SpyPaymentGateway(
                getResultFactory:
                    externalReference =>
                        Result<PaymentGatewayResponse>
                            .Success(
                                new PaymentGatewayResponse(
                                    externalReference,
                                    PaymentGatewayStatus.Succeeded)));

        var coordinator =
            CreateCoordinator(
                gateway);

        // Act
        Result<PaymentCancellationOutcome> result =
            await coordinator
                .EnsurePaymentCannotSucceedAsync(
                    payment,
                    booking,
                    TestContext.Current.CancellationToken);

        // Assert
        Assert.True(
            result.IsSuccess);

        Assert.Equal(
            PaymentCancellationOutcome.PaymentSucceeded,
            result.Value);

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
            gateway.GetCallCount);

        Assert.Equal(
            0,
            gateway.CancelCallCount);
    }

    [Theory]
    [InlineData(PaymentGatewayStatus.Failed)]
    [InlineData(PaymentGatewayStatus.Cancelled)]
    public async Task EnsurePaymentCannotSucceedAsync_WhenProviderAlreadyTerminal_ShouldReconcileAndReturnSafe(
        PaymentGatewayStatus observedStatus)
    {
        // Arrange
        DomainBooking booking =
            CreatePendingPaymentBooking();

        (Payment payment, PaymentAttempt attempt) =
            CreatePendingPayment(
                booking,
                "provider-terminal");

        var gateway =
            new SpyPaymentGateway(
                getResultFactory:
                    externalReference =>
                        Result<PaymentGatewayResponse>
                            .Success(
                                new PaymentGatewayResponse(
                                    externalReference,
                                    observedStatus)));

        var coordinator =
            CreateCoordinator(
                gateway);

        // Act
        Result<PaymentCancellationOutcome> result =
            await coordinator
                .EnsurePaymentCannotSucceedAsync(
                    payment,
                    booking,
                    TestContext.Current.CancellationToken);

        // Assert
        Assert.True(
            result.IsSuccess);

        Assert.Equal(
            PaymentCancellationOutcome.SafeToCancelBooking,
            result.Value);

        Assert.Equal(
            BookingStatus.PendingPayment,
            booking.Status);

        Assert.Equal(
            observedStatus == PaymentGatewayStatus.Failed
                ? PaymentStatus.Failed
                : PaymentStatus.Cancelled,
            payment.Status);

        Assert.Equal(
            observedStatus == PaymentGatewayStatus.Failed
                ? PaymentAttemptStatus.Failed
                : PaymentAttemptStatus.Cancelled,
            attempt.Status);

        Assert.Equal(
            1,
            gateway.GetCallCount);

        Assert.Equal(
            0,
            gateway.CancelCallCount);
    }

    [Fact]
    public async Task EnsurePaymentCannotSucceedAsync_WhenProviderIsPending_ShouldCancelProviderPaymentAndReturnSafe()
    {
        // Arrange
        DomainBooking booking =
            CreatePendingPaymentBooking();

        (Payment payment, PaymentAttempt attempt) =
            CreatePendingPayment(
                booking,
                "provider-pending");

        var gateway =
            new SpyPaymentGateway();

        var coordinator =
            CreateCoordinator(
                gateway);

        // Act
        Result<PaymentCancellationOutcome> result =
            await coordinator
                .EnsurePaymentCannotSucceedAsync(
                    payment,
                    booking,
                    TestContext.Current.CancellationToken);

        // Assert
        Assert.True(
            result.IsSuccess);

        Assert.Equal(
            PaymentCancellationOutcome.SafeToCancelBooking,
            result.Value);

        Assert.Equal(
            BookingStatus.PendingPayment,
            booking.Status);

        Assert.Equal(
            PaymentStatus.Cancelled,
            payment.Status);

        Assert.Equal(
            PaymentAttemptStatus.Cancelled,
            attempt.Status);

        Assert.Equal(
            1,
            gateway.GetCallCount);

        Assert.Equal(
            1,
            gateway.CancelCallCount);
    }

    [Fact]
    public async Task EnsurePaymentCannotSucceedAsync_WhenProviderCancellationFails_ShouldReturnFailureWithoutLocalCancellation()
    {
        // Arrange
        DomainBooking booking =
            CreatePendingPaymentBooking();

        (Payment payment, PaymentAttempt attempt) =
            CreatePendingPayment(
                booking,
                "cancel-failure");

        var gateway =
            new SpyPaymentGateway(
                cancelResultFactory:
                    _ =>
                        Result<PaymentGatewayResponse>
                            .Failure(
                                PaymentGatewayErrors.ProviderTimeout));

        var coordinator =
            CreateCoordinator(
                gateway);

        // Act
        Result<PaymentCancellationOutcome> result =
            await coordinator
                .EnsurePaymentCannotSucceedAsync(
                    payment,
                    booking,
                    TestContext.Current.CancellationToken);

        // Assert
        Assert.True(
            result.IsFailure);

        Assert.Equal(
            PaymentGatewayErrors.ProviderTimeout,
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
            gateway.GetCallCount);

        Assert.Equal(
            1,
            gateway.CancelCallCount);
    }

    [Fact]
    public async Task EnsurePaymentCannotSucceedAsync_WhenProviderSucceedsDuringCancellation_ShouldReconcileAndReturnPaymentSucceeded()
    {
        // Arrange
        DomainBooking booking =
            CreatePendingPaymentBooking();

        (Payment payment, PaymentAttempt attempt) =
            CreatePendingPayment(
                booking,
                "succeeds-during-cancel");

        var gateway =
            new SpyPaymentGateway(
                cancelResultFactory:
                    externalReference =>
                        Result<PaymentGatewayResponse>
                            .Success(
                                new PaymentGatewayResponse(
                                    externalReference,
                                    PaymentGatewayStatus.Succeeded)));

        var coordinator =
            CreateCoordinator(
                gateway);

        // Act
        Result<PaymentCancellationOutcome> result =
            await coordinator
                .EnsurePaymentCannotSucceedAsync(
                    payment,
                    booking,
                    TestContext.Current.CancellationToken);

        // Assert
        Assert.True(
            result.IsSuccess);

        Assert.Equal(
            PaymentCancellationOutcome.PaymentSucceeded,
            result.Value);

        Assert.Equal(
            BookingStatus.Paid,
            booking.Status);

        Assert.Equal(
            PaymentStatus.Succeeded,
            payment.Status);

        Assert.Equal(
            PaymentAttemptStatus.Succeeded,
            attempt.Status);
    }

    [Fact]
    public async Task EnsurePaymentCannotSucceedAsync_WhenProviderCancellationRemainsPending_ShouldReturnNotConfirmed()
    {
        // Arrange
        DomainBooking booking =
            CreatePendingPaymentBooking();

        (Payment payment, PaymentAttempt attempt) =
            CreatePendingPayment(
                booking,
                "still-pending");

        var gateway =
            new SpyPaymentGateway(
                cancelResultFactory:
                    externalReference =>
                        Result<PaymentGatewayResponse>
                            .Success(
                                new PaymentGatewayResponse(
                                    externalReference,
                                    PaymentGatewayStatus.Pending)));

        var coordinator =
            CreateCoordinator(
                gateway);

        // Act
        Result<PaymentCancellationOutcome> result =
            await coordinator
                .EnsurePaymentCannotSucceedAsync(
                    payment,
                    booking,
                    TestContext.Current.CancellationToken);

        // Assert
        Assert.True(
            result.IsFailure);

        Assert.Equal(
            PaymentCancellationErrors
                .CancellationNotConfirmed(
                    attempt.ExternalReference,
                    PaymentGatewayStatus.Pending),
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
    }

    [Fact]
    public async Task EnsurePaymentCannotSucceedAsync_WhenProviderReturnsDifferentReference_ShouldReturnFailure()
    {
        // Arrange
        DomainBooking booking =
            CreatePendingPaymentBooking();

        (Payment payment, PaymentAttempt attempt) =
            CreatePendingPayment(
                booking,
                "expected-reference");

        var gateway =
            new SpyPaymentGateway(
                getResultFactory:
                    _ =>
                        Result<PaymentGatewayResponse>
                            .Success(
                                new PaymentGatewayResponse(
                                    "different-reference",
                                    PaymentGatewayStatus.Pending)));

        var coordinator =
            CreateCoordinator(
                gateway);

        // Act
        Result<PaymentCancellationOutcome> result =
            await coordinator
                .EnsurePaymentCannotSucceedAsync(
                    payment,
                    booking,
                    TestContext.Current.CancellationToken);

        // Assert
        Assert.True(
            result.IsFailure);

        Assert.Equal(
            PaymentCancellationErrors
                .ProviderReferenceMismatch(
                    attempt.ExternalReference,
                    "different-reference"),
            result.Error);

        Assert.Equal(
            PaymentStatus.Pending,
            payment.Status);

        Assert.Equal(
            PaymentAttemptStatus.Pending,
            attempt.Status);

        Assert.Equal(
            0,
            gateway.CancelCallCount);
    }

    private static PaymentCancellationCoordinator
        CreateCoordinator(
            IPaymentGateway paymentGateway)
    {
        return new PaymentCancellationCoordinator(
            paymentGateway,
            new StubClock(
                UtcNow));
    }

    private static DomainBooking
        CreatePendingPaymentBooking()
    {
        DomainBooking booking =
            CreateBooking();

        Result approveResult =
            booking.Approve();

        Assert.True(
            approveResult.IsSuccess);

        return booking;
    }

    private static DomainBooking
        CreateBooking()
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

        return DomainBooking.Create(
            rentableUnit,
            stayPeriod,
            GuestCount.Create(
                2)
            .Value, GuestDetails.Create("John Doe", "john@example.com", "+50377778888").Value)
            .Value;
    }

    private static Payment CreatePayment(
        DomainBooking booking)
    {
        return Payment.Create(
            booking.Id,
            Money.Create(
                200m,
                "USD")
            .Value,
            UtcNow.AddMinutes(-2))
            .Value;
    }

    private static (
        Payment Payment,
        PaymentAttempt Attempt)
        CreatePendingPayment(
            DomainBooking booking,
            string externalReference)
    {
        Payment payment =
            CreatePayment(
                booking);

        Result<PaymentAttempt> attemptResult =
            payment.AddAttempt(
                $"operation-{Guid.NewGuid():N}",
                externalReference,
                UtcNow.AddMinutes(-1));

        Assert.True(
            attemptResult.IsSuccess);

        return (
            payment,
            attemptResult.Value);
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

    private sealed class SpyPaymentGateway
        : IPaymentGateway
    {
        private readonly Func<
            string,
            Result<PaymentGatewayResponse>>
            _getResultFactory;

        private readonly Func<
            string,
            Result<PaymentGatewayResponse>>
            _cancelResultFactory;

        public SpyPaymentGateway(
            Func<
                string,
                Result<PaymentGatewayResponse>>?
                getResultFactory = null,
            Func<
                string,
                Result<PaymentGatewayResponse>>?
                cancelResultFactory = null)
        {
            _getResultFactory =
                getResultFactory ??
                (
                    externalReference =>
                        Result<PaymentGatewayResponse>
                            .Success(
                                new PaymentGatewayResponse(
                                    externalReference,
                                    PaymentGatewayStatus.Pending)));

            _cancelResultFactory =
                cancelResultFactory ??
                (
                    externalReference =>
                        Result<PaymentGatewayResponse>
                            .Success(
                                new PaymentGatewayResponse(
                                    externalReference,
                                    PaymentGatewayStatus.Cancelled)));
        }

        public int GetCallCount
        {
            get;
            private set;
        }

        public int CancelCallCount
        {
            get;
            private set;
        }

        public Task<Result<CreatePaymentAttemptResponse>>
            CreatePaymentAttemptAsync(
                CreatePaymentAttemptRequest request,
                CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<Result<PaymentGatewayResponse>>
            GetPaymentStatusAsync(
                string externalReference,
                CancellationToken cancellationToken = default)
        {
            cancellationToken
                .ThrowIfCancellationRequested();

            GetCallCount++;

            return Task.FromResult(
                _getResultFactory(
                    externalReference));
        }

        public Task<Result<PaymentGatewayResponse>>
            CancelPaymentAsync(
                string externalReference,
                CancellationToken cancellationToken = default)
        {
            cancellationToken
                .ThrowIfCancellationRequested();

            CancelCallCount++;

            return Task.FromResult(
                _cancelResultFactory(
                    externalReference));
        }
    }
}
