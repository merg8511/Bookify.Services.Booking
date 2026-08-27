using Bookify.Services.Booking.Application.Abstractions.Payments;
using Bookify.Services.Booking.Application.Abstractions.Persistence;
using Bookify.Services.Booking.Application.Abstractions.Persistence.Repositories;
using Bookify.Services.Booking.Application.Abstractions.Time;
using Bookify.Services.Booking.Application.Payments.Initiate;
using Bookify.Services.Booking.Domain.Bookings;
using Bookify.Services.Booking.Domain.Bookings.Pricing;
using Bookify.Services.Booking.Domain.Bookings.ValueObjects;
using Bookify.Services.Booking.Domain.Payments;
using Bookify.Services.Booking.Domain.Payments.Errors;
using Bookify.Services.Booking.Domain.Properties;
using Bookify.Services.Booking.Domain.Shared;
using Bookify.Services.Booking.Domain.Shared.ValueObjects;

using DomainBooking = Bookify.Services.Booking.Domain.Bookings.Booking;

namespace Bookify.Services.Booking.Application.Tests.Payments.Initiate;

public sealed class InitiatePaymentCommandHandlerTests
{
    private static readonly DateTimeOffset UtcNow =
        new(
            2026,
            8,
            25,
            18,
            0,
            0,
            TimeSpan.Zero);

    [Fact]
    public async Task HandleAsync_WhenBookingDoesNotExist_ShouldReturnNotFound()
    {
        // Arrange
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        Guid bookingId = Guid.NewGuid();

        var bookingRepository = new StubBookingRepository();
        var paymentRepository = new SpyPaymentRepository();
        var paymentGateway = new SpyPaymentGateway();
        var unitOfWork = new SpyUnitOfWork();

        var handler =
            CreateHandler(
                bookingRepository,
                paymentRepository,
                paymentGateway,
                unitOfWork);

        var command = new InitiatePaymentCommand(bookingId, "operation-001");

        // Act
        Result<InitiatePaymentResponse> result = await handler.HandleAsync(command, cancellationToken);

        // Assert
        Assert.True(result.IsFailure);

        Assert.Equal(
            InitiatePaymentErrors.BookingNotFound(
                bookingId),
            result.Error);

        Assert.Equal(
            0,
            paymentGateway.CreateCallCount);

        Assert.Equal(
            0,
            unitOfWork.SaveChangesCallCount);
    }

    [Fact]
    public async Task HandleAsync_WhenBookingIsNotPendingPayment_ShouldReturnConflict()
    {
        // Arrange
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        DomainBooking booking =
            CreateBooking(
                approve: false);

        var bookingRepository =
            new StubBookingRepository(
                booking);

        var paymentRepository =
            new SpyPaymentRepository();

        var paymentGateway =
            new SpyPaymentGateway();

        var unitOfWork =
            new SpyUnitOfWork();

        var handler =
            CreateHandler(
                bookingRepository,
                paymentRepository,
                paymentGateway,
                unitOfWork);

        var command =
            new InitiatePaymentCommand(
                booking.Id,
                "operation-001");

        // Act
        Result<InitiatePaymentResponse> result =
            await handler.HandleAsync(
                command,
                cancellationToken);

        // Assert
        Assert.True(
            result.IsFailure);

        Assert.Equal(
            InitiatePaymentErrors
                .BookingNotPendingPayment(
                    BookingStatus.PendingApproval),
            result.Error);

        Assert.Equal(
            0,
            paymentGateway.CreateCallCount);

        Assert.Null(
            paymentRepository.AddedPayment);
    }

    [Fact]
    public async Task HandleAsync_WithEmptyIdempotencyKey_ShouldReturnValidationError()
    {
        // Arrange
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        DomainBooking booking =
            CreateBooking();

        var bookingRepository =
            new StubBookingRepository(
                booking);

        var paymentRepository =
            new SpyPaymentRepository();

        var paymentGateway =
            new SpyPaymentGateway();

        var unitOfWork =
            new SpyUnitOfWork();

        var handler =
            CreateHandler(
                bookingRepository,
                paymentRepository,
                paymentGateway,
                unitOfWork);

        var command =
            new InitiatePaymentCommand(
                booking.Id,
                "   ");

        // Act
        Result<InitiatePaymentResponse> result =
            await handler.HandleAsync(
                command,
                cancellationToken);

        // Assert
        Assert.True(
            result.IsFailure);

        Assert.Equal(
            InitiatePaymentErrors
                .IdempotencyKeyRequired,
            result.Error);

        Assert.Equal(
            0,
            paymentGateway.CreateCallCount);

        Assert.Equal(
            0,
            unitOfWork.SaveChangesCallCount);
    }

    [Fact]
    public async Task HandleAsync_WithValidBooking_ShouldCreatePaymentAndAttempt()
    {
        // Arrange
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        DomainBooking booking =
            CreateBooking();

        var bookingRepository =
            new StubBookingRepository(
                booking);

        var paymentRepository =
            new SpyPaymentRepository();

        var paymentGateway =
            new SpyPaymentGateway(
                new PaymentGatewayResponse(
                    "fake_external_001",
                    PaymentGatewayStatus.Pending));

        var unitOfWork =
            new SpyUnitOfWork();

        var handler =
            CreateHandler(
                bookingRepository,
                paymentRepository,
                paymentGateway,
                unitOfWork);

        var command =
            new InitiatePaymentCommand(
                booking.Id,
                "operation-001");

        // Act
        Result<InitiatePaymentResponse> result =
            await handler.HandleAsync(
                command,
                cancellationToken);

        // Assert
        Assert.True(
            result.IsSuccess);

        Assert.NotNull(
            paymentRepository.AddedPayment);

        Payment payment =
            paymentRepository.AddedPayment!;

        Assert.Equal(
            booking.Id,
            payment.BookingId);

        Assert.Single(
            payment.Attempts);

        PaymentAttempt attempt =
            Assert.Single(
                payment.Attempts);

        Assert.Equal(
            "fake_external_001",
            attempt.ExternalReference);

        Assert.Equal(
            PaymentAttemptStatus.Pending,
            attempt.Status);

        Assert.Equal(
            payment.Id,
            result.Value.PaymentId);

        Assert.Equal(
            attempt.Id,
            result.Value.PaymentAttemptId);

        Assert.Equal(
            attempt.ExternalReference,
            result.Value.ExternalReference);

        Assert.Equal(
            PaymentAttemptStatus.Pending,
            result.Value.Status);

        Assert.Equal(
            payment.Amount.Amount,
            result.Value.Amount);

        Assert.Equal(
            payment.Amount.Currency,
            result.Value.Currency);

        Assert.Equal(
            1,
            paymentGateway.CreateCallCount);

        Assert.Equal(
            2,
            unitOfWork.SaveChangesCallCount);
    }

    [Fact]
    public async Task HandleAsync_WhenGatewayReturnsSucceeded_ShouldPersistSucceededAttempt()
    {
        // Arrange
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        DomainBooking booking =
            CreateBooking();

        var bookingRepository =
            new StubBookingRepository(
                booking);

        var paymentRepository =
            new SpyPaymentRepository();

        var paymentGateway =
            new SpyPaymentGateway(
                new PaymentGatewayResponse(
                    "fake_external_succeeded",
                    PaymentGatewayStatus.Succeeded));

        var unitOfWork =
            new SpyUnitOfWork();

        var handler =
            CreateHandler(
                bookingRepository,
                paymentRepository,
                paymentGateway,
                unitOfWork);

        var command =
            new InitiatePaymentCommand(
                booking.Id,
                "operation-succeeded");

        // Act
        Result<InitiatePaymentResponse> result =
            await handler.HandleAsync(
                command,
                cancellationToken);

        // Assert
        Assert.True(
            result.IsSuccess);

        Payment payment =
            Assert.IsType<Payment>(
                paymentRepository.AddedPayment);

        PaymentAttempt attempt =
            Assert.Single(
                payment.Attempts);

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
            PaymentAttemptStatus.Succeeded,
            result.Value.Status);
    }

    [Fact]
    public async Task HandleAsync_WhenGatewayReturnsFailed_ShouldPersistFailedAttempt()
    {
        // Arrange
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        DomainBooking booking =
            CreateBooking();

        var bookingRepository =
            new StubBookingRepository(
                booking);

        var paymentRepository =
            new SpyPaymentRepository();

        var paymentGateway =
            new SpyPaymentGateway(
                new PaymentGatewayResponse(
                    "fake_external_failed",
                    PaymentGatewayStatus.Failed));

        var unitOfWork =
            new SpyUnitOfWork();

        var handler =
            CreateHandler(
                bookingRepository,
                paymentRepository,
                paymentGateway,
                unitOfWork);

        var command =
            new InitiatePaymentCommand(
                booking.Id,
                "operation-failed");

        // Act
        Result<InitiatePaymentResponse> result =
            await handler.HandleAsync(
                command,
                cancellationToken);

        // Assert
        Assert.True(
            result.IsSuccess);

        Payment payment =
            Assert.IsType<Payment>(
                paymentRepository.AddedPayment);

        PaymentAttempt attempt =
            Assert.Single(
                payment.Attempts);

        Assert.Equal(
            PaymentStatus.Failed,
            payment.Status);

        Assert.Equal(
            PaymentAttemptStatus.Failed,
            attempt.Status);

        Assert.Null(
            payment.CompletedAtUtc);

        Assert.Equal(
            UtcNow,
            attempt.CompletedAtUtc);

        Assert.Equal(
            PaymentAttemptStatus.Failed,
            result.Value.Status);
    }

    [Fact]
    public async Task HandleAsync_WhenGatewayReturnsCancelled_ShouldPersistCancelledAttempt()
    {
        // Arrange
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        DomainBooking booking =
            CreateBooking();

        var bookingRepository =
            new StubBookingRepository(
                booking);

        var paymentRepository =
            new SpyPaymentRepository();

        var paymentGateway =
            new SpyPaymentGateway(
                new PaymentGatewayResponse(
                    "fake_external_cancelled",
                    PaymentGatewayStatus.Cancelled));

        var unitOfWork =
            new SpyUnitOfWork();

        var handler =
            CreateHandler(
                bookingRepository,
                paymentRepository,
                paymentGateway,
                unitOfWork);

        var command =
            new InitiatePaymentCommand(
                booking.Id,
                "operation-cancelled");

        // Act
        Result<InitiatePaymentResponse> result =
            await handler.HandleAsync(
                command,
                cancellationToken);

        // Assert
        Assert.True(
            result.IsSuccess);

        Payment payment =
            Assert.IsType<Payment>(
                paymentRepository.AddedPayment);

        PaymentAttempt attempt =
            Assert.Single(
                payment.Attempts);

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
            PaymentAttemptStatus.Cancelled,
            result.Value.Status);
    }

    [Fact]
    public async Task HandleAsync_WhenGatewayFails_ShouldPersistPaymentWithoutAttempt()
    {
        // Arrange
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        DomainBooking booking =
            CreateBooking();

        var bookingRepository =
            new StubBookingRepository(
                booking);

        var paymentRepository =
            new SpyPaymentRepository();

        var paymentGateway =
            new SpyPaymentGateway(
                PaymentGatewayErrors.ProviderTimeout);

        var unitOfWork =
            new SpyUnitOfWork();

        var handler =
            CreateHandler(
                bookingRepository,
                paymentRepository,
                paymentGateway,
                unitOfWork);

        var command =
            new InitiatePaymentCommand(
                booking.Id,
                "operation-001");

        // Act
        Result<InitiatePaymentResponse> result =
            await handler.HandleAsync(
                command,
                cancellationToken);

        // Assert
        Assert.True(
            result.IsFailure);

        Assert.Equal(
            PaymentGatewayErrors.ProviderTimeout,
            result.Error);

        Assert.NotNull(
            paymentRepository.AddedPayment);

        Assert.Empty(
            paymentRepository
                .AddedPayment!
                .Attempts);

        Assert.Equal(
            1,
            paymentGateway.CreateCallCount);

        Assert.Equal(
            1,
            unitOfWork.SaveChangesCallCount);
    }

    [Fact]
    public async Task HandleAsync_WithSameIdempotencyKey_ShouldReturnExistingAttemptWithoutCallingGateway()
    {
        // Arrange
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        DomainBooking booking =
            CreateBooking();

        Payment payment =
            CreatePayment(
                booking);

        string operationKey =
            CreateExpectedOperationKey(
                booking.Id,
                "operation-001");

        Result<PaymentAttempt> attemptResult =
            payment.AddAttempt(
                operationKey,
                "fake_external_001",
                UtcNow);

        Assert.True(
            attemptResult.IsSuccess);

        var bookingRepository =
            new StubBookingRepository(
                booking);

        var paymentRepository =
            new SpyPaymentRepository(
                payment);

        var paymentGateway =
            new SpyPaymentGateway();

        var unitOfWork =
            new SpyUnitOfWork();

        var handler =
            CreateHandler(
                bookingRepository,
                paymentRepository,
                paymentGateway,
                unitOfWork);

        var command =
            new InitiatePaymentCommand(
                booking.Id,
                "operation-001");

        // Act
        Result<InitiatePaymentResponse> result =
            await handler.HandleAsync(
                command,
                cancellationToken);

        // Assert
        Assert.True(
            result.IsSuccess);

        Assert.Equal(
            payment.Id,
            result.Value.PaymentId);

        Assert.Equal(
            attemptResult.Value.Id,
            result.Value.PaymentAttemptId);

        Assert.Equal(
            "fake_external_001",
            result.Value.ExternalReference);

        Assert.Equal(
            0,
            paymentGateway.CreateCallCount);

        Assert.Equal(
            0,
            unitOfWork.SaveChangesCallCount);

        Assert.Single(
            payment.Attempts);
    }

    [Fact]
    public async Task HandleAsync_WithDifferentKeyAndPendingAttempt_ShouldReturnConflict()
    {
        // Arrange
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        DomainBooking booking =
            CreateBooking();

        Payment payment =
            CreatePayment(
                booking);

        Result<PaymentAttempt> attemptResult =
            payment.AddAttempt(
                "existing-operation",
                "fake_external_001",
                UtcNow);

        Assert.True(
            attemptResult.IsSuccess);

        var bookingRepository =
            new StubBookingRepository(
                booking);

        var paymentRepository =
            new SpyPaymentRepository(
                payment);

        var paymentGateway =
            new SpyPaymentGateway();

        var unitOfWork =
            new SpyUnitOfWork();

        var handler =
            CreateHandler(
                bookingRepository,
                paymentRepository,
                paymentGateway,
                unitOfWork);

        var command =
            new InitiatePaymentCommand(
                booking.Id,
                "different-operation");

        // Act
        Result<InitiatePaymentResponse> result =
            await handler.HandleAsync(
                command,
                cancellationToken);

        // Assert
        Assert.True(
            result.IsFailure);

        Assert.Equal(
            PaymentErrors.ActiveAttemptAlreadyExists,
            result.Error);

        Assert.Equal(
            0,
            paymentGateway.CreateCallCount);

        Assert.Equal(
            0,
            unitOfWork.SaveChangesCallCount);
    }

    private static InitiatePaymentCommandHandler
        CreateHandler(
            IBookingRepository bookingRepository,
            IPaymentRepository paymentRepository,
            IPaymentGateway paymentGateway,
            IUnitOfWork unitOfWork)
    {
        return new InitiatePaymentCommandHandler(
            bookingRepository,
            paymentRepository,
            paymentGateway,
            unitOfWork,
            new StubTransactionManager(),
            new StubPaymentInitiationLock(),
            new StubClock(UtcNow));
    }

    private static Payment CreatePayment(
        DomainBooking booking)
    {
        Result<Payment> result =
            Payment.Create(
                booking.Id,
                booking.PriceSnapshot!.TotalPrice,
                UtcNow.AddMinutes(-1));

        Assert.True(
            result.IsSuccess);

        return result.Value;
    }

    private static DomainBooking CreateBooking(
        bool approve = true)
    {
        RentableUnit rentableUnit =
            CreateRentableUnit();

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

        GuestCount guestCount =
            GuestCount.Create(
                2)
            .Value;

        PriceSnapshot priceSnapshot =
            CreatePriceSnapshot();

        Result<DomainBooking> bookingResult =
            DomainBooking.Create(
                rentableUnit,
                stayPeriod,
                guestCount,
                priceSnapshot);

        Assert.True(
            bookingResult.IsSuccess);

        DomainBooking booking =
            bookingResult.Value;

        if (approve)
        {
            Result approveResult =
                booking.Approve();

            Assert.True(
                approveResult.IsSuccess);
        }

        return booking;
    }

    private static RentableUnit
        CreateRentableUnit()
    {
        return RentableUnit.Create(
            Guid.NewGuid(),
            "Room A",
            RentableUnitType.Room,
            maximumCapacity: 4,
            maxBaseGuests: 2)
            .Value;
    }

    private static PriceSnapshot
        CreatePriceSnapshot()
    {
        Money accommodationPrice =
            Money.Create(
                200m,
                "USD")
            .Value;

        Money extraGuestPrice =
            Money.Create(
                0m,
                "USD")
            .Value;

        Money totalPrice =
            Money.Create(
                200m,
                "USD")
            .Value;

        return PriceSnapshot.Create(
            new PriceBreakdown(accommodationPrice,
            extraGuestPrice,
            totalPrice));
    }

    private static string
        CreateExpectedOperationKey(
            Guid bookingId,
            string idempotencyKey)
    {
        string value =
            $"{bookingId:N}:{idempotencyKey}";

        byte[] bytes =
            System.Text.Encoding.UTF8
                .GetBytes(
                    value);

        byte[] hash =
            System.Security.Cryptography
                .SHA256.HashData(
                    bytes);

        return
            $"bookify-payment-{Convert.ToHexString(hash).ToLowerInvariant()}";
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

            DomainBooking? result =
                _booking?.Id ==
                bookingId
                    ? _booking
                    : null;

            return Task.FromResult(
                result);
        }

        public void Add(
            DomainBooking booking)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class SpyPaymentRepository
        : IPaymentRepository
    {
        private readonly Payment? _payment;

        public SpyPaymentRepository(
            Payment? payment = null)
        {
            _payment =
                payment;
        }

        public Payment? AddedPayment
        {
            get;
            private set;
        }

        public Task<Payment?>
            GetByBookingIdAsync(
                Guid bookingId,
                CancellationToken cancellationToken = default)
        {
            cancellationToken
                .ThrowIfCancellationRequested();

            Payment? result =
                _payment?.BookingId ==
                bookingId
                    ? _payment
                    : null;

            return Task.FromResult(
                result);
        }

        public void Add(
            Payment payment)
        {
            AddedPayment =
                payment;
        }
    }

    private sealed class SpyPaymentGateway
        : IPaymentGateway
    {
        private readonly
            PaymentGatewayResponse?
            _createResponse;

        private readonly Error?
            _createError;

        public SpyPaymentGateway()
        {
        }

        public SpyPaymentGateway(
            PaymentGatewayResponse
                createResponse)
        {
            _createResponse =
                createResponse;
        }

        public SpyPaymentGateway(
            Error createError)
        {
            _createError =
                createError;
        }

        public int CreateCallCount
        {
            get;
            private set;
        }

        public Task<
            Result<PaymentGatewayResponse>>
            CreatePaymentAttemptAsync(
                CreatePaymentAttemptRequest request,
                CancellationToken cancellationToken = default)
        {
            cancellationToken
                .ThrowIfCancellationRequested();

            CreateCallCount++;

            if (_createError is not null)
            {
                return Task.FromResult(
                    Result<PaymentGatewayResponse>
                        .Failure(
                            _createError));
            }

            PaymentGatewayResponse response =
                _createResponse
                ?? new PaymentGatewayResponse(
                    "fake_default",
                    PaymentGatewayStatus.Pending);

            return Task.FromResult(
                Result<PaymentGatewayResponse>
                    .Success(
                        response));
        }

        public Task<
            Result<PaymentGatewayResponse>>
            GetPaymentStatusAsync(
                string externalReference,
                CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<
            Result<PaymentGatewayResponse>>
            CancelPaymentAsync(
                string externalReference,
                CancellationToken cancellationToken = default)
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

    private sealed class StubPaymentInitiationLock
    : IPaymentInitiationLock
    {
        public Task<bool> TryAcquireAsync(
            Guid bookingId,
            CancellationToken cancellationToken = default)
        {
            cancellationToken
                .ThrowIfCancellationRequested();

            return Task.FromResult(true);
        }
    }

    private sealed class StubTransactionManager
        : ITransactionManager
    {
        public Task<ITransaction> BeginAsync(
            CancellationToken cancellationToken = default)
        {
            cancellationToken
                .ThrowIfCancellationRequested();

            return Task.FromResult<ITransaction>(
                new StubTransaction());
        }
    }

    private sealed class StubTransaction
        : ITransaction
    {
        public Task CommitAsync(
            CancellationToken cancellationToken = default)
        {
            cancellationToken
                .ThrowIfCancellationRequested();

            return Task.CompletedTask;
        }

        public Task RollbackAsync(
            CancellationToken cancellationToken = default)
        {
            cancellationToken
                .ThrowIfCancellationRequested();

            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            return ValueTask.CompletedTask;
        }
    }
}
