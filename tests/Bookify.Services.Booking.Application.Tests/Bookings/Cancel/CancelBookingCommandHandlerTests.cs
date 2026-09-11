using Bookify.Services.Booking.Application.Abstractions.Payments;
using Bookify.Services.Booking.Application.Abstractions.Persistence;
using Bookify.Services.Booking.Application.Abstractions.Persistence.Repositories;
using Bookify.Services.Booking.Application.Abstractions.Time;
using Bookify.Services.Booking.Application.Bookings.Cancel;
using Bookify.Services.Booking.Application.Payments.Cancellation;
using Bookify.Services.Booking.Application.Payments.Initiate;
using Bookify.Services.Booking.Domain.Bookings;
using Bookify.Services.Booking.Domain.Bookings.ValueObjects;
using Bookify.Services.Booking.Domain.Payments;
using Bookify.Services.Booking.Domain.Properties;
using Bookify.Services.Booking.Domain.Shared;
using Bookify.Services.Booking.Domain.Shared.ValueObjects;

using DomainBooking =
    Bookify.Services.Booking.Domain.Bookings.Booking;

namespace Bookify.Services.Booking.Application.Tests.Bookings.Cancel;

public sealed class CancelBookingCommandHandlerTests
{
    private static readonly DateTimeOffset UtcNow =
        new(
            2026,
            9,
            5,
            20,
            0,
            0,
            TimeSpan.Zero);

    [Fact]
    public async Task HandleAsync_WhenBookingIsPendingApproval_ShouldCancelAndSave()
    {
        // Arrange
        CancellationToken cancellationToken =
            TestContext.Current.CancellationToken;

        DomainBooking booking =
            CreateBooking();

        var gateway =
            new SpyPaymentGateway();

        var unitOfWork =
            new SpyUnitOfWork();

        var transactionManager =
            new SpyTransactionManager();

        CancelBookingCommandHandler handler =
            CreateHandler(
                new StubBookingRepository(
                    booking),
                new StubPaymentRepository(),
                gateway,
                unitOfWork,
                transactionManager);

        // Act
        Result result =
            await handler.HandleAsync(
                new CancelBookingCommand(
                    booking.Id),
                cancellationToken);

        // Assert
        Assert.True(
            result.IsSuccess);

        Assert.Equal(
            BookingStatus.Cancelled,
            booking.Status);

        Assert.Equal(
            BookingCancellationReason.CancelledByGuest,
            booking.CancellationReason);

        Assert.False(
            booking.BlocksInventory);

        Assert.Equal(
            0,
            gateway.GetCallCount);

        Assert.Equal(
            0,
            gateway.CancelCallCount);

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

    [Fact]
    public async Task HandleAsync_WhenBookingDoesNotExist_ShouldReturnNotFoundWithoutSaving()
    {
        // Arrange
        CancellationToken cancellationToken =
            TestContext.Current.CancellationToken;

        Guid bookingId =
            Guid.NewGuid();

        var gateway =
            new SpyPaymentGateway();

        var unitOfWork =
            new SpyUnitOfWork();

        var transactionManager =
            new SpyTransactionManager();

        CancelBookingCommandHandler handler =
            CreateHandler(
                new StubBookingRepository(),
                new StubPaymentRepository(),
                gateway,
                unitOfWork,
                transactionManager);

        // Act
        Result result =
            await handler.HandleAsync(
                new CancelBookingCommand(
                    bookingId),
                cancellationToken);

        // Assert
        Assert.True(
            result.IsFailure);

        Assert.Equal(
            CancelBookingErrors.NotFound(
                bookingId),
            result.Error);

        Assert.Equal(
            0,
            gateway.GetCallCount);

        Assert.Equal(
            0,
            gateway.CancelCallCount);

        Assert.Equal(
            0,
            unitOfWork.SaveChangesCallCount);

        Assert.Equal(
            0,
            transactionManager.Transaction.CommitCallCount);

        Assert.Equal(
            1,
            transactionManager.Transaction.RollbackCallCount);
    }

    [Fact]
    public async Task HandleAsync_WhenBookingIsPaid_ShouldReturnConflictWithoutSaving()
    {
        // Arrange
        CancellationToken cancellationToken =
            TestContext.Current.CancellationToken;

        DomainBooking booking =
            CreatePendingPaymentBooking();

        Result paidResult =
            booking.MarkAsPaid();

        Assert.True(
            paidResult.IsSuccess);

        var gateway =
            new SpyPaymentGateway();

        var unitOfWork =
            new SpyUnitOfWork();

        var transactionManager =
            new SpyTransactionManager();

        CancelBookingCommandHandler handler =
            CreateHandler(
                new StubBookingRepository(
                    booking),
                new StubPaymentRepository(),
                gateway,
                unitOfWork,
                transactionManager);

        // Act
        Result result =
            await handler.HandleAsync(
                new CancelBookingCommand(
                    booking.Id),
                cancellationToken);

        // Assert
        Assert.True(
            result.IsFailure);

        Assert.Equal(
            "Booking.InvalidStatusTransition",
            result.Error.Code);

        Assert.Equal(
            ErrorType.Conflict,
            result.Error.Type);

        Assert.Equal(
            BookingStatus.Paid,
            booking.Status);

        Assert.Null(
            booking.CancellationReason);

        Assert.Equal(
            0,
            gateway.GetCallCount);

        Assert.Equal(
            0,
            gateway.CancelCallCount);

        Assert.Equal(
            0,
            unitOfWork.SaveChangesCallCount);

        Assert.Equal(
            0,
            transactionManager.Transaction.CommitCallCount);

        Assert.Equal(
            1,
            transactionManager.Transaction.RollbackCallCount);
    }

    [Fact]
    public async Task HandleAsync_WhenPendingPaymentHasNoPayment_ShouldCancelWithoutCallingGateway()
    {
        // Arrange
        CancellationToken cancellationToken =
            TestContext.Current.CancellationToken;

        DomainBooking booking =
            CreatePendingPaymentBooking();

        var gateway =
            new SpyPaymentGateway();

        var unitOfWork =
            new SpyUnitOfWork();

        var transactionManager =
            new SpyTransactionManager();

        CancelBookingCommandHandler handler =
            CreateHandler(
                new StubBookingRepository(
                    booking),
                new StubPaymentRepository(),
                gateway,
                unitOfWork,
                transactionManager);

        // Act
        Result result =
            await handler.HandleAsync(
                new CancelBookingCommand(
                    booking.Id),
                cancellationToken);

        // Assert
        Assert.True(
            result.IsSuccess);

        Assert.Equal(
            BookingStatus.Cancelled,
            booking.Status);

        Assert.Equal(
            BookingCancellationReason.CancelledByGuest,
            booking.CancellationReason);

        Assert.Equal(
            0,
            gateway.GetCallCount);

        Assert.Equal(
            0,
            gateway.CancelCallCount);

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

    [Fact]
    public async Task HandleAsync_WhenPendingProviderPaymentExists_ShouldCancelProviderAndBooking()
    {
        // Arrange
        CancellationToken cancellationToken =
            TestContext.Current.CancellationToken;

        DomainBooking booking =
            CreatePendingPaymentBooking();

        (
            Payment payment,
            PaymentAttempt attempt) =
                CreatePendingPayment(
                    booking,
                    "pending-provider-payment");

        var gateway =
            new SpyPaymentGateway();

        var unitOfWork =
            new SpyUnitOfWork();

        var transactionManager =
            new SpyTransactionManager();

        CancelBookingCommandHandler handler =
            CreateHandler(
                new StubBookingRepository(
                    booking),
                new StubPaymentRepository(
                    payment),
                gateway,
                unitOfWork,
                transactionManager);

        // Act
        Result result =
            await handler.HandleAsync(
                new CancelBookingCommand(
                    booking.Id),
                cancellationToken);

        // Assert
        Assert.True(
            result.IsSuccess);

        Assert.Equal(
            BookingStatus.Cancelled,
            booking.Status);

        Assert.Equal(
            BookingCancellationReason.CancelledByGuest,
            booking.CancellationReason);

        Assert.Equal(
            PaymentStatus.Cancelled,
            payment.Status);

        Assert.Equal(
            PaymentAttemptStatus.Cancelled,
            attempt.Status);

        Assert.Equal(
            UtcNow,
            payment.CompletedAtUtc);

        Assert.Equal(
            UtcNow,
            attempt.CompletedAtUtc);

        Assert.Equal(
            1,
            gateway.GetCallCount);

        Assert.Equal(
            1,
            gateway.CancelCallCount);

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

    [Fact]
    public async Task HandleAsync_WhenProviderAlreadySucceeded_ShouldPersistPaidStateAndRejectCancellation()
    {
        // Arrange
        CancellationToken cancellationToken =
            TestContext.Current.CancellationToken;

        DomainBooking booking =
            CreatePendingPaymentBooking();

        (
            Payment payment,
            PaymentAttempt attempt) =
                CreatePendingPayment(
                    booking,
                    "already-succeeded");

        var gateway =
            new SpyPaymentGateway(
                getResultFactory:
                    externalReference =>
                        Result<PaymentGatewayResponse>
                            .Success(
                                new PaymentGatewayResponse(
                                    externalReference,
                                    PaymentGatewayStatus.Succeeded)));

        var unitOfWork =
            new SpyUnitOfWork();

        var transactionManager =
            new SpyTransactionManager();

        CancelBookingCommandHandler handler =
            CreateHandler(
                new StubBookingRepository(
                    booking),
                new StubPaymentRepository(
                    payment),
                gateway,
                unitOfWork,
                transactionManager);

        // Act
        Result result =
            await handler.HandleAsync(
                new CancelBookingCommand(
                    booking.Id),
                cancellationToken);

        // Assert
        Assert.True(
            result.IsFailure);

        Assert.Equal(
            CancelBookingErrors
                .PaymentAlreadySucceeded(
                    booking.Id),
            result.Error);

        Assert.Equal(
            BookingStatus.Paid,
            booking.Status);

        Assert.Null(
            booking.CancellationReason);

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
            gateway.GetCallCount);

        Assert.Equal(
            0,
            gateway.CancelCallCount);

        //
        // Aunque el comando Cancel falla, acabamos de descubrir
        // que el proveedor ya cobró.
        //
        // Esa verdad sí debe persistirse.
        //
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

    [Fact]
    public async Task HandleAsync_WhenProviderStatusCannotBeRead_ShouldNotCancelBooking()
    {
        // Arrange
        CancellationToken cancellationToken =
            TestContext.Current.CancellationToken;

        DomainBooking booking =
            CreatePendingPaymentBooking();

        (
            Payment payment,
            PaymentAttempt attempt) =
                CreatePendingPayment(
                    booking,
                    "provider-query-failure");

        var gateway =
            new SpyPaymentGateway(
                getResultFactory:
                    _ =>
                        Result<PaymentGatewayResponse>
                            .Failure(
                                PaymentGatewayErrors.ProviderTimeout));

        var unitOfWork =
            new SpyUnitOfWork();

        var transactionManager =
            new SpyTransactionManager();

        CancelBookingCommandHandler handler =
            CreateHandler(
                new StubBookingRepository(
                    booking),
                new StubPaymentRepository(
                    payment),
                gateway,
                unitOfWork,
                transactionManager);

        // Act
        Result result =
            await handler.HandleAsync(
                new CancelBookingCommand(
                    booking.Id),
                cancellationToken);

        // Assert
        Assert.True(
            result.IsFailure);

        Assert.Equal(
            PaymentGatewayErrors.ProviderTimeout,
            result.Error);

        Assert.Equal(
            BookingStatus.PendingPayment,
            booking.Status);

        Assert.Null(
            booking.CancellationReason);

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

        Assert.Equal(
            0,
            unitOfWork.SaveChangesCallCount);

        Assert.Equal(
            0,
            transactionManager.Transaction.CommitCallCount);

        Assert.Equal(
            1,
            transactionManager.Transaction.RollbackCallCount);
    }

    [Fact]
    public async Task HandleAsync_WhenProviderCancellationFails_ShouldNotCancelBooking()
    {
        // Arrange
        CancellationToken cancellationToken =
            TestContext.Current.CancellationToken;

        DomainBooking booking =
            CreatePendingPaymentBooking();

        (
            Payment payment,
            PaymentAttempt attempt) =
                CreatePendingPayment(
                    booking,
                    "provider-cancellation-failure");

        var gateway =
            new SpyPaymentGateway(
                cancelResultFactory:
                    _ =>
                        Result<PaymentGatewayResponse>
                            .Failure(
                                PaymentGatewayErrors.ProviderTimeout));

        var unitOfWork =
            new SpyUnitOfWork();

        var transactionManager =
            new SpyTransactionManager();

        CancelBookingCommandHandler handler =
            CreateHandler(
                new StubBookingRepository(
                    booking),
                new StubPaymentRepository(
                    payment),
                gateway,
                unitOfWork,
                transactionManager);

        // Act
        Result result =
            await handler.HandleAsync(
                new CancelBookingCommand(
                    booking.Id),
                cancellationToken);

        // Assert
        Assert.True(
            result.IsFailure);

        Assert.Equal(
            PaymentGatewayErrors.ProviderTimeout,
            result.Error);

        Assert.Equal(
            BookingStatus.PendingPayment,
            booking.Status);

        Assert.Null(
            booking.CancellationReason);

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

        Assert.Equal(
            0,
            unitOfWork.SaveChangesCallCount);

        Assert.Equal(
            0,
            transactionManager.Transaction.CommitCallCount);

        Assert.Equal(
            1,
            transactionManager.Transaction.RollbackCallCount);
    }

    [Fact]
    public async Task HandleAsync_WhenPaymentStateIsUncertain_ShouldNotCancelBooking()
    {
        // Arrange
        CancellationToken cancellationToken =
            TestContext.Current.CancellationToken;

        DomainBooking booking =
            CreatePendingPaymentBooking();

        Payment payment =
            CreatePayment(
                booking);

        var gateway =
            new SpyPaymentGateway();

        var unitOfWork =
            new SpyUnitOfWork();

        var transactionManager =
            new SpyTransactionManager();

        CancelBookingCommandHandler handler =
            CreateHandler(
                new StubBookingRepository(
                    booking),
                new StubPaymentRepository(
                    payment),
                gateway,
                unitOfWork,
                transactionManager);

        // Act
        Result result =
            await handler.HandleAsync(
                new CancelBookingCommand(
                    booking.Id),
                cancellationToken);

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

        Assert.Null(
            booking.CancellationReason);

        Assert.Equal(
            0,
            gateway.GetCallCount);

        Assert.Equal(
            0,
            gateway.CancelCallCount);

        Assert.Equal(
            0,
            unitOfWork.SaveChangesCallCount);

        Assert.Equal(
            0,
            transactionManager.Transaction.CommitCallCount);

        Assert.Equal(
            1,
            transactionManager.Transaction.RollbackCallCount);
    }

    [Fact]
    public async Task HandleAsync_WithNullCommand_ShouldThrow()
    {
        // Arrange
        CancelBookingCommandHandler handler =
            CreateHandler(
                new StubBookingRepository(),
                new StubPaymentRepository(),
                new SpyPaymentGateway(),
                new SpyUnitOfWork(),
                new SpyTransactionManager());

        // Act
        Task Action()
        {
            return handler.HandleAsync(
                null!);
        }

        // Assert
        await Assert.ThrowsAsync<
            ArgumentNullException>(
                Action);
    }

    private static CancelBookingCommandHandler
        CreateHandler(
            IBookingRepository bookingRepository,
            IPaymentRepository paymentRepository,
            IPaymentGateway paymentGateway,
            IUnitOfWork unitOfWork,
            ITransactionManager transactionManager)
    {
        var paymentCancellationCoordinator =
            new PaymentCancellationCoordinator(
                paymentGateway,
                new StubClock(
                    UtcNow));

        return new CancelBookingCommandHandler(
            bookingRepository,
            paymentRepository,
            paymentCancellationCoordinator,
            unitOfWork,
            transactionManager,
            new StubPaymentInitiationLock());
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

        Assert.Equal(
            BookingStatus.PendingPayment,
            booking.Status);

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
            .Value)
            .Value;
    }

    private static Payment CreatePayment(
        DomainBooking booking)
    {
        Result<Payment> paymentResult =
            Payment.Create(
                booking.Id,
                Money.Create(
                    200m,
                    "USD")
                .Value,
                UtcNow.AddMinutes(-2));

        Assert.True(
            paymentResult.IsSuccess);

        return paymentResult.Value;
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

    private sealed class StubBookingRepository
        : IBookingRepository
    {
        private readonly DomainBooking? _booking;

        public StubBookingRepository(
            DomainBooking? booking = null)
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

            DomainBooking? booking =
                _booking?.Id ==
                bookingId
                    ? _booking
                    : null;

            return Task.FromResult(
                booking);
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
            Payment? payment = null)
        {
            _payment =
                payment;
        }

        public Task<Payment?> GetByBookingIdAsync(
            Guid bookingId,
            CancellationToken cancellationToken = default)
        {
            cancellationToken
                .ThrowIfCancellationRequested();

            Payment? payment =
                _payment?.BookingId ==
                bookingId
                    ? _payment
                    : null;

            return Task.FromResult(
                payment);
        }

        public void Add(
            Payment payment)
        {
            throw new NotSupportedException();
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

    private sealed class SpyTransactionManager
        : ITransactionManager
    {
        public SpyTransaction Transaction
        {
            get;
        } = new();

        public Task<ITransaction> BeginAsync(
            CancellationToken cancellationToken = default)
        {
            cancellationToken
                .ThrowIfCancellationRequested();

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
