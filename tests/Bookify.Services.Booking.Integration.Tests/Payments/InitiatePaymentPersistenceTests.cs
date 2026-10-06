using Bookify.Services.Booking.Application.Abstractions.Messaging;
using Bookify.Services.Booking.Application.Abstractions.Payments;
using Bookify.Services.Booking.Application.Payments.Initiate;
using Bookify.Services.Booking.Application.Payments.Reconciliation;
using Bookify.Services.Booking.Domain.Bookings;
using Bookify.Services.Booking.Domain.Bookings.Pricing;
using Bookify.Services.Booking.Domain.Payments;
using Bookify.Services.Booking.Domain.Payments.Errors;
using Bookify.Services.Booking.Domain.Shared;
using Bookify.Services.Booking.Infrastructure.Persistence;
using Bookify.Services.Booking.Integration.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using DomainBooking = Bookify.Services.Booking.Domain.Bookings.Booking;

namespace Bookify.Services.Booking.Integration.Tests.Payments;

[Collection(BookingApiTestFixture.Name)]
public sealed class InitiatePaymentPersistenceTests
{
    private readonly BookingApiFactory _factory;

    public InitiatePaymentPersistenceTests(BookingApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task ExecuteAsync_WithPendingPaymentBooking_ShouldPersistPaymentAndAttempt()
    {
        // Arrange
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        Guid bookingId = await CreatePendingPaymentBookingAsync();
        string idempotencyKey = $"payment-initiation-{Guid.NewGuid():N}";

        Guid paymentId;
        Guid paymentAttemptId;
        string externalReference;

        // Act
        using (IServiceScope writeScope = _factory.Services.CreateScope())
        {
            var executor = writeScope.ServiceProvider.GetRequiredService<ICommandExecutor<InitiatePaymentCommand, InitiatePaymentResponse>>();
            var command = new InitiatePaymentCommand(bookingId, idempotencyKey);

            Result<InitiatePaymentResponse> result = await executor.ExecuteAsync(command, cancellationToken);

            // Assert application result
            Assert.True(result.IsSuccess);
            Assert.NotEqual(Guid.Empty, result.Value.PaymentId);
            Assert.NotEqual(Guid.Empty, result.Value.PaymentAttemptId);
            Assert.StartsWith("fake_", result.Value.ExternalReference, StringComparison.Ordinal);
            Assert.Equal(PaymentAttemptStatus.Pending, result.Value.Status);
            Assert.False(string.IsNullOrWhiteSpace(result.Value.ClientSecret));

            paymentId = result.Value.PaymentId;
            paymentAttemptId = result.Value.PaymentAttemptId;
            externalReference = result.Value.ExternalReference;
        }

        // Assert persisted state from a fresh scope
        using (IServiceScope readScope = _factory.Services.CreateScope())
        {
            BookingDbContext dbContext = readScope.ServiceProvider.GetRequiredService<BookingDbContext>();

            Payment? persistedPayment = await dbContext.Payments.AsNoTracking()
                .SingleOrDefaultAsync(payment => payment.Id == paymentId, cancellationToken);

            Assert.NotNull(persistedPayment);
            Assert.Equal(bookingId, persistedPayment.BookingId);
            Assert.Equal(PaymentStatus.Pending, persistedPayment.Status);
            Assert.Null(persistedPayment.CompletedAtUtc);

            PaymentAttempt? persistedAttempt = await dbContext.PaymentAttempts.AsNoTracking()
                .SingleOrDefaultAsync(attempt => attempt.Id == paymentAttemptId, cancellationToken);

            Assert.NotNull(persistedAttempt);
            Assert.Equal(paymentId, persistedAttempt.PaymentId);
            Assert.Equal(externalReference, persistedAttempt.ExternalReference);
            Assert.Equal(PaymentAttemptStatus.Pending, persistedAttempt.Status);
            Assert.Null(persistedAttempt.CompletedAtUtc);
        }
    }

    [Fact]
    public async Task ExecuteAsync_WithSameIdempotencyKey_ShouldReturnExistingAttemptWithoutCreatingDuplicate()
    {
        // Arrange
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        Guid bookingId = await CreatePendingPaymentBookingAsync();
        string idempotencyKey = $"payment-initiation-idempotent-{Guid.NewGuid():N}";

        InitiatePaymentResponse firstResponse;

        using (IServiceScope firstScope = _factory.Services.CreateScope())
        {
            var executor = firstScope.ServiceProvider.GetRequiredService<ICommandExecutor<InitiatePaymentCommand, InitiatePaymentResponse>>();
            var command = new InitiatePaymentCommand(bookingId, idempotencyKey);

            Result<InitiatePaymentResponse> firstResult = await executor.ExecuteAsync(command, cancellationToken);
            Assert.True(firstResult.IsSuccess);
            firstResponse = firstResult.Value;
        }

        // Act: simulate HTTP/process retry using a new scope
        using (IServiceScope retryScope = _factory.Services.CreateScope())
        {
            var executor = retryScope.ServiceProvider.GetRequiredService<ICommandExecutor<InitiatePaymentCommand, InitiatePaymentResponse>>();
            var command = new InitiatePaymentCommand(bookingId, idempotencyKey);

            Result<InitiatePaymentResponse> retryResult = await executor.ExecuteAsync(command, cancellationToken);

            // Assert
            Assert.True(retryResult.IsSuccess);
            Assert.Equal(firstResponse.PaymentId, retryResult.Value.PaymentId);
            Assert.Equal(firstResponse.PaymentAttemptId, retryResult.Value.PaymentAttemptId);
            Assert.Equal(firstResponse.ExternalReference, retryResult.Value.ExternalReference);
            Assert.Equal(firstResponse.ClientSecret, retryResult.Value.ClientSecret);
        }

        // Assert physical rows
        using (IServiceScope verificationScope = _factory.Services.CreateScope())
        {
            BookingDbContext dbContext = verificationScope.ServiceProvider.GetRequiredService<BookingDbContext>();

            int paymentCount = await dbContext.Payments.AsNoTracking()
                .CountAsync(payment => payment.BookingId == bookingId, cancellationToken);

            int attemptCount = await dbContext.PaymentAttempts.AsNoTracking()
                .Where(attempt => attempt.PaymentId == firstResponse.PaymentId)
                .CountAsync(cancellationToken);

            Assert.Equal(1, paymentCount);
            Assert.Equal(1, attemptCount);
        }
    }

    [Fact]
    public async Task ExecuteAsync_WithConcurrentDifferentIdempotencyKeys_ShouldCreateOnlyOnePendingAttempt()
    {
        // Arrange
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        Guid bookingId = await CreatePendingPaymentBookingAsync();

        using IServiceScope firstScope = _factory.Services.CreateScope();
        using IServiceScope secondScope = _factory.Services.CreateScope();

        var firstExecutor = firstScope.ServiceProvider.GetRequiredService<ICommandExecutor<InitiatePaymentCommand, InitiatePaymentResponse>>();
        var secondExecutor = secondScope.ServiceProvider.GetRequiredService<ICommandExecutor<InitiatePaymentCommand, InitiatePaymentResponse>>();

        var firstCommand = new InitiatePaymentCommand(bookingId, $"concurrent-payment-{Guid.NewGuid():N}");
        var secondCommand = new InitiatePaymentCommand(bookingId, $"concurrent-payment-{Guid.NewGuid():N}");

        var startGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        async Task<Result<InitiatePaymentResponse>> ExecuteAsync(
            ICommandExecutor<InitiatePaymentCommand, InitiatePaymentResponse> executor,
            InitiatePaymentCommand command)
        {
            await startGate.Task;
            return await executor.ExecuteAsync(command, cancellationToken);
        }

        Task<Result<InitiatePaymentResponse>> firstTask = ExecuteAsync(firstExecutor, firstCommand);
        Task<Result<InitiatePaymentResponse>> secondTask = ExecuteAsync(secondExecutor, secondCommand);

        // Act
        startGate.SetResult();
        Result<InitiatePaymentResponse>[] results = await Task.WhenAll(firstTask, secondTask);

        // Assert application results
        Result<InitiatePaymentResponse> successResult = Assert.Single(results, result => result.IsSuccess);
        Result<InitiatePaymentResponse> failureResult = Assert.Single(results, result => result.IsFailure);

        Assert.Equal(PaymentErrors.ActiveAttemptAlreadyExists, failureResult.Error);
        Assert.Equal(PaymentAttemptStatus.Pending, successResult.Value.Status);

        // Assert persisted state from a fresh scope
        using IServiceScope verificationScope = _factory.Services.CreateScope();
        BookingDbContext dbContext = verificationScope.ServiceProvider.GetRequiredService<BookingDbContext>();

        Payment persistedPayment = await dbContext.Payments.AsNoTracking()
            .SingleAsync(payment => payment.BookingId == bookingId, cancellationToken);

        List<PaymentAttempt> persistedAttempts = await dbContext.PaymentAttempts.AsNoTracking()
            .Where(attempt => attempt.PaymentId == persistedPayment.Id)
            .ToListAsync(cancellationToken);

        PaymentAttempt persistedAttempt = Assert.Single(persistedAttempts);
        Assert.Equal(PaymentAttemptStatus.Pending, persistedAttempt.Status);
        Assert.Equal(successResult.Value.PaymentId, persistedPayment.Id);
        Assert.Equal(successResult.Value.PaymentAttemptId, persistedAttempt.Id);
        Assert.Equal(successResult.Value.ExternalReference, persistedAttempt.ExternalReference);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldUseBookingPriceSnapshotAsPaymentAmount()
    {
        // Arrange
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        Guid bookingId = await CreatePendingPaymentBookingAsync();

        // Act
        using (IServiceScope scope = _factory.Services.CreateScope())
        {
            var executor = scope.ServiceProvider.GetRequiredService<ICommandExecutor<InitiatePaymentCommand, InitiatePaymentResponse>>();
            var command = new InitiatePaymentCommand(bookingId, $"payment-price-{Guid.NewGuid():N}");

            Result<InitiatePaymentResponse> result = await executor.ExecuteAsync(command, cancellationToken);

            // Assert
            Assert.True(result.IsSuccess);
            Assert.Equal(200m, result.Value.Amount);
            Assert.Equal("USD", result.Value.Currency);
        }

        using (IServiceScope verificationScope = _factory.Services.CreateScope())
        {
            BookingDbContext dbContext = verificationScope.ServiceProvider.GetRequiredService<BookingDbContext>();
            Payment payment = await dbContext.Payments.AsNoTracking()
                .SingleAsync(payment => payment.BookingId == bookingId, cancellationToken);

            Assert.Equal(200m, payment.Amount.Amount);
            Assert.Equal("USD", payment.Amount.Currency);
        }
    }

    [Fact]
    public async Task ExecuteAsync_WithCancelledAttempt_ShouldCreateNewAttemptAndSetPaymentToPending()
    {
        // Arrange
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        Guid bookingId = await CreatePendingPaymentBookingAsync();
        Guid paymentId;

        DateTimeOffset paymentCreatedAtUtc = DateTimeOffset.UtcNow;
        DateTimeOffset cancelledAtUtc = paymentCreatedAtUtc.AddMinutes(1);
        string previousIdempotencyKey = $"cancelled-payment-{Guid.NewGuid():N}";
        string previousExternalReference = $"cancelled-external-{Guid.NewGuid():N}";

        // Arrange persisted cancelled payment
        using (IServiceScope setupScope = _factory.Services.CreateScope())
        {
            BookingDbContext dbContext = setupScope.ServiceProvider.GetRequiredService<BookingDbContext>();

            Result<Payment> paymentResult = Payment.Create(
                bookingId,
                CreatePriceSnapshot().TotalPrice,
                paymentCreatedAtUtc);

            Assert.True(paymentResult.IsSuccess);
            Payment payment = paymentResult.Value;

            Result<PaymentAttempt> attemptResult = payment.AddAttempt(
                previousIdempotencyKey,
                previousExternalReference,
                paymentCreatedAtUtc);

            Assert.True(attemptResult.IsSuccess);

            Result cancelResult = payment.CancelAttempt(previousExternalReference, cancelledAtUtc);
            Assert.True(cancelResult.IsSuccess);
            Assert.Equal(PaymentStatus.Cancelled, payment.Status);
            Assert.Equal(cancelledAtUtc, payment.CompletedAtUtc);
            Assert.Equal(PaymentAttemptStatus.Cancelled, attemptResult.Value.Status);

            dbContext.Payments.Add(payment);
            await dbContext.SaveChangesAsync(cancellationToken);

            paymentId = payment.Id;
        }

        // Act
        using (IServiceScope executionScope = _factory.Services.CreateScope())
        {
            var executor = executionScope.ServiceProvider.GetRequiredService<ICommandExecutor<InitiatePaymentCommand, InitiatePaymentResponse>>();
            string retryIdempotencyKey = $"retry-payment-{Guid.NewGuid():N}";
            var command = new InitiatePaymentCommand(bookingId, retryIdempotencyKey);

            Result<InitiatePaymentResponse> result = await executor.ExecuteAsync(command, cancellationToken);

            Assert.True(result.IsSuccess, result.IsFailure ? $"Unexpected error: {result.Error.Code} - {result.Error.Message}" : string.Empty);
            Assert.Equal(paymentId, result.Value.PaymentId);
            Assert.Equal(PaymentAttemptStatus.Pending, result.Value.Status);
            Assert.False(string.IsNullOrWhiteSpace(result.Value.ClientSecret));
            Assert.NotEqual(previousExternalReference, result.Value.ExternalReference);
        }

        // Assert persisted retry state
        using (IServiceScope verificationScope = _factory.Services.CreateScope())
        {
            BookingDbContext dbContext = verificationScope.ServiceProvider.GetRequiredService<BookingDbContext>();

            Payment payment = await dbContext.Payments
                .Include(persistedPayment => persistedPayment.Attempts)
                .AsNoTracking()
                .SingleAsync(persistedPayment => persistedPayment.Id == paymentId, cancellationToken);

            Assert.Equal(PaymentStatus.Pending, payment.Status);
            Assert.Null(payment.CompletedAtUtc);
            Assert.Equal(2, payment.Attempts.Count);

            List<PaymentAttempt> attempts = payment.Attempts
                .OrderBy(attempt => attempt.CreatedAtUtc)
                .ToList();

            PaymentAttempt cancelledAttempt = attempts[0];
            PaymentAttempt retryAttempt = attempts[1];

            Assert.Equal(PaymentAttemptStatus.Cancelled, cancelledAttempt.Status);
            Assert.NotNull(cancelledAttempt.CompletedAtUtc);
            Assert.Equal(previousExternalReference, cancelledAttempt.ExternalReference);
            Assert.Equal(PaymentAttemptStatus.Pending, retryAttempt.Status);
            Assert.Null(retryAttempt.CompletedAtUtc);
            Assert.NotEqual(cancelledAttempt.Id, retryAttempt.Id);
            Assert.NotEqual(cancelledAttempt.ExternalReference, retryAttempt.ExternalReference);
        }
    }

    [Fact]
    public async Task ReconcileSucceeded_ShouldPersistPaymentAttemptPaymentAndBookingAsPaid()
    {
        // Arrange
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        Guid bookingId = await CreatePendingPaymentBookingAsync();
        Guid paymentId;
        Guid paymentAttemptId;

        DateTimeOffset paymentCreatedAtUtc = new(2026, 9, 4, 19, 59, 0, TimeSpan.Zero);
        DateTimeOffset attemptCreatedAtUtc = new(2026, 9, 4, 20, 0, 0, TimeSpan.Zero);
        DateTimeOffset succeededAtUtc = new(2026, 9, 4, 20, 1, 0, TimeSpan.Zero);

        string idempotencyKey = $"reconciliation-{Guid.NewGuid():N}";
        string externalReference = $"reconciliation-external-{Guid.NewGuid():N}";

        using (IServiceScope reconciliationScope = _factory.Services.CreateScope())
        {
            BookingDbContext dbContext = reconciliationScope.ServiceProvider.GetRequiredService<BookingDbContext>();

            DomainBooking booking = await dbContext.Bookings.SingleAsync(b => b.Id == bookingId, cancellationToken);
            Assert.Equal(BookingStatus.PendingPayment, booking.Status);

            Result<Payment> paymentResult = Payment.Create(booking.Id, CreatePriceSnapshot().TotalPrice, paymentCreatedAtUtc);
            Assert.True(paymentResult.IsSuccess);

            Payment payment = paymentResult.Value;
            Result<PaymentAttempt> attemptResult = payment.AddAttempt(idempotencyKey, externalReference, attemptCreatedAtUtc);
            Assert.True(attemptResult.IsSuccess);

            PaymentAttempt attempt = attemptResult.Value;

            // Act
            Result reconciliationResult = PaymentReconciler.Reconcile(
                payment,
                attempt,
                booking,
                PaymentGatewayStatus.Succeeded,
                succeededAtUtc);

            // Assert in-memory reconciliation before persistence
            Assert.True(reconciliationResult.IsSuccess);
            Assert.Equal(BookingStatus.Paid, booking.Status);
            Assert.Equal(PaymentStatus.Succeeded, payment.Status);
            Assert.Equal(PaymentAttemptStatus.Succeeded, attempt.Status);

            dbContext.Payments.Add(payment);
            await dbContext.SaveChangesAsync(cancellationToken);

            paymentId = payment.Id;
            paymentAttemptId = attempt.Id;
        }

        // Assert persisted state using a completely fresh scope.
        using (IServiceScope verificationScope = _factory.Services.CreateScope())
        {
            BookingDbContext dbContext = verificationScope.ServiceProvider.GetRequiredService<BookingDbContext>();

            DomainBooking persistedBooking = await dbContext.Bookings.AsNoTracking()
                .SingleAsync(b => b.Id == bookingId, cancellationToken);

            Payment persistedPayment = await dbContext.Payments.AsNoTracking()
                .SingleAsync(payment => payment.Id == paymentId, cancellationToken);

            PaymentAttempt persistedAttempt = await dbContext.PaymentAttempts.AsNoTracking()
                .SingleAsync(attempt => attempt.Id == paymentAttemptId, cancellationToken);

            Assert.Equal(BookingStatus.Paid, persistedBooking.Status);
            Assert.Equal(PaymentStatus.Succeeded, persistedPayment.Status);
            Assert.Equal(succeededAtUtc, persistedPayment.CompletedAtUtc);
            Assert.Equal(PaymentAttemptStatus.Succeeded, persistedAttempt.Status);
            Assert.Equal(succeededAtUtc, persistedAttempt.CompletedAtUtc);
            Assert.Equal(externalReference, persistedAttempt.ExternalReference);
        }
    }

    private async Task<Guid> CreatePendingPaymentBookingAsync()
    {
        var booking = await BookingDatabaseTestSeeder.SeedBookingAsync(
            _factory.Services,
            status: BookingStatus.PendingPayment);

        return booking.Id;
    }

    private static PriceSnapshot CreatePriceSnapshot() => BookingTestData.CreatePriceSnapshot();
}
