using Bookify.Services.Booking.Application.Abstractions.Messaging;
using Bookify.Services.Booking.Application.Abstractions.Persistence;
using Bookify.Services.Booking.Application.Abstractions.Persistence.Repositories;
using Bookify.Services.Booking.Application.Payments.Initiate;
using Bookify.Services.Booking.Domain.Bookings;
using Bookify.Services.Booking.Domain.Bookings.Pricing;
using Bookify.Services.Booking.Domain.Bookings.ValueObjects;
using Bookify.Services.Booking.Domain.Payments;
using Bookify.Services.Booking.Domain.Payments.Errors;
using Bookify.Services.Booking.Domain.Properties;
using Bookify.Services.Booking.Domain.Shared;
using Bookify.Services.Booking.Domain.Shared.ValueObjects;
using Bookify.Services.Booking.Infrastructure.Persistence;
using Bookify.Services.Booking.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using DomainBooking =
    Bookify.Services.Booking.Domain.Bookings.Booking;

namespace Bookify.Services.Booking.IntegrationTests.Payments;

[Collection(BookingApiTestFixture.Name)]
public sealed class InitiatePaymentPersistenceTests
{
    private readonly BookingApiFactory _factory;

    public InitiatePaymentPersistenceTests(
        BookingApiFactory factory)
    {
        _factory =
            factory;
    }

    [Fact]
    public async Task ExecuteAsync_WithPendingPaymentBooking_ShouldPersistPaymentAndAttempt()
    {
        // Arrange
        CancellationToken cancellationToken =
            TestContext.Current.CancellationToken;

        Guid bookingId =
            await CreatePendingPaymentBookingAsync();

        const string idempotencyKey =
            "payment-initiation-001";

        Guid paymentId;
        Guid paymentAttemptId;
        string externalReference;

        // Act
        using (
            IServiceScope writeScope =
                _factory.Services
                    .CreateScope())
        {
            ICommandExecutor<
                InitiatePaymentCommand,
                InitiatePaymentResponse> executor =
                    writeScope.ServiceProvider
                        .GetRequiredService<
                            ICommandExecutor<
                                InitiatePaymentCommand,
                                InitiatePaymentResponse>>();

            var command =
                new InitiatePaymentCommand(
                    bookingId,
                    idempotencyKey);

            Result<InitiatePaymentResponse> result =
                await executor.ExecuteAsync(
                    command,
                    cancellationToken);

            // Assert application result
            Assert.True(
                result.IsSuccess);

            Assert.NotEqual(
                Guid.Empty,
                result.Value.PaymentId);

            Assert.NotEqual(
                Guid.Empty,
                result.Value.PaymentAttemptId);

            Assert.StartsWith(
                "fake_",
                result.Value.ExternalReference,
                StringComparison.Ordinal);

            Assert.Equal(
                PaymentAttemptStatus.Pending,
                result.Value.Status);

            paymentId =
                result.Value.PaymentId;

            paymentAttemptId =
                result.Value.PaymentAttemptId;

            externalReference =
                result.Value.ExternalReference;
        }

        // Assert persisted state from a fresh scope
        using (
            IServiceScope readScope =
                _factory.Services
                    .CreateScope())
        {
            BookingDbContext dbContext =
                readScope.ServiceProvider
                    .GetRequiredService<
                        BookingDbContext>();

            Payment? persistedPayment =
                await dbContext.Payments
                    .AsNoTracking()
                    .SingleOrDefaultAsync(
                        payment =>
                            payment.Id ==
                            paymentId,
                        cancellationToken);

            Assert.NotNull(
                persistedPayment);

            Assert.Equal(
                bookingId,
                persistedPayment.BookingId);

            Assert.Equal(
                PaymentStatus.Pending,
                persistedPayment.Status);

            PaymentAttempt? persistedAttempt =
                await dbContext.PaymentAttempts
                    .AsNoTracking()
                    .SingleOrDefaultAsync(
                        attempt =>
                            attempt.Id ==
                            paymentAttemptId,
                        cancellationToken);

            Assert.NotNull(
                persistedAttempt);

            Assert.Equal(
                paymentId,
                persistedAttempt.PaymentId);

            Assert.Equal(
                externalReference,
                persistedAttempt.ExternalReference);

            Assert.Equal(
                PaymentAttemptStatus.Pending,
                persistedAttempt.Status);

            Assert.Null(
                persistedAttempt.CompletedAtUtc);
        }
    }

    [Fact]
    public async Task ExecuteAsync_WithSameIdempotencyKey_ShouldReturnExistingAttemptWithoutCreatingDuplicate()
    {
        // Arrange
        CancellationToken cancellationToken =
            TestContext.Current.CancellationToken;

        Guid bookingId =
            await CreatePendingPaymentBookingAsync();

        const string idempotencyKey =
            "payment-initiation-idempotent-001";

        InitiatePaymentResponse firstResponse;

        using (
            IServiceScope firstScope =
                _factory.Services
                    .CreateScope())
        {
            ICommandExecutor<
                InitiatePaymentCommand,
                InitiatePaymentResponse> executor =
                    firstScope.ServiceProvider
                        .GetRequiredService<
                            ICommandExecutor<
                                InitiatePaymentCommand,
                                InitiatePaymentResponse>>();

            var command =
                new InitiatePaymentCommand(
                    bookingId,
                    idempotencyKey);

            Result<InitiatePaymentResponse> firstResult =
                await executor.ExecuteAsync(
                    command,
                    cancellationToken);

            Assert.True(
                firstResult.IsSuccess);

            firstResponse =
                firstResult.Value;
        }

        // Act: simulate HTTP/process retry using a new scope
        using (
            IServiceScope retryScope =
                _factory.Services
                    .CreateScope())
        {
            ICommandExecutor<
                InitiatePaymentCommand,
                InitiatePaymentResponse> executor =
                    retryScope.ServiceProvider
                        .GetRequiredService<
                            ICommandExecutor<
                                InitiatePaymentCommand,
                                InitiatePaymentResponse>>();

            var command =
                new InitiatePaymentCommand(
                    bookingId,
                    idempotencyKey);

            Result<InitiatePaymentResponse> retryResult =
                await executor.ExecuteAsync(
                    command,
                    cancellationToken);

            // Assert
            Assert.True(
                retryResult.IsSuccess);

            Assert.Equal(
                firstResponse.PaymentId,
                retryResult.Value.PaymentId);

            Assert.Equal(
                firstResponse.PaymentAttemptId,
                retryResult.Value.PaymentAttemptId);

            Assert.Equal(
                firstResponse.ExternalReference,
                retryResult.Value.ExternalReference);
        }

        // Assert physical rows
        using (
            IServiceScope verificationScope =
                _factory.Services
                    .CreateScope())
        {
            BookingDbContext dbContext =
                verificationScope.ServiceProvider
                    .GetRequiredService<
                        BookingDbContext>();

            int paymentCount =
                await dbContext.Payments
                    .AsNoTracking()
                    .CountAsync(
                        payment =>
                            payment.BookingId ==
                            bookingId,
                        cancellationToken);

            int attemptCount =
                await dbContext.PaymentAttempts
                    .AsNoTracking()
                    .Where(
                        attempt =>
                            attempt.PaymentId ==
                            firstResponse.PaymentId)
                    .CountAsync(
                        cancellationToken);

            Assert.Equal(
                1,
                paymentCount);

            Assert.Equal(
                1,
                attemptCount);
        }
    }

    [Fact]
    public async Task ExecuteAsync_WithConcurrentDifferentIdempotencyKeys_ShouldCreateOnlyOnePendingAttempt()
    {
        // Arrange
        CancellationToken cancellationToken =
            TestContext.Current.CancellationToken;

        Guid bookingId =
            await CreatePendingPaymentBookingAsync();

        using IServiceScope firstScope =
            _factory.Services
                .CreateScope();

        using IServiceScope secondScope =
            _factory.Services
                .CreateScope();

        ICommandExecutor<
            InitiatePaymentCommand,
            InitiatePaymentResponse> firstExecutor =
                firstScope.ServiceProvider
                    .GetRequiredService<
                        ICommandExecutor<
                            InitiatePaymentCommand,
                            InitiatePaymentResponse>>();

        ICommandExecutor<
            InitiatePaymentCommand,
            InitiatePaymentResponse> secondExecutor =
                secondScope.ServiceProvider
                    .GetRequiredService<
                        ICommandExecutor<
                            InitiatePaymentCommand,
                            InitiatePaymentResponse>>();

        var firstCommand =
            new InitiatePaymentCommand(
                bookingId,
                "concurrent-payment-001");

        var secondCommand =
            new InitiatePaymentCommand(
                bookingId,
                "concurrent-payment-002");

        var startGate =
            new TaskCompletionSource(
                TaskCreationOptions
                    .RunContinuationsAsynchronously);

        async Task<Result<InitiatePaymentResponse>>
            ExecuteAsync(
                ICommandExecutor<
                    InitiatePaymentCommand,
                    InitiatePaymentResponse> executor,
                InitiatePaymentCommand command)
        {
            await startGate.Task;

            return await executor.ExecuteAsync(
                command,
                cancellationToken);
        }

        Task<Result<InitiatePaymentResponse>>
            firstTask =
                ExecuteAsync(
                    firstExecutor,
                    firstCommand);

        Task<Result<InitiatePaymentResponse>>
            secondTask =
                ExecuteAsync(
                    secondExecutor,
                    secondCommand);

        // Act
        startGate.SetResult();

        Result<InitiatePaymentResponse>[] results =
            await Task.WhenAll(
                firstTask,
                secondTask);

        // Assert application results
        Result<InitiatePaymentResponse> successResult =
            Assert.Single(
                results,
                result =>
                    result.IsSuccess);

        Result<InitiatePaymentResponse> failureResult =
            Assert.Single(
                results,
                result =>
                    result.IsFailure);

        Assert.Equal(
            PaymentErrors.ActiveAttemptAlreadyExists,
            failureResult.Error);

        Assert.Equal(
            PaymentAttemptStatus.Pending,
            successResult.Value.Status);

        // Assert persisted state from a fresh scope
        using IServiceScope verificationScope =
            _factory.Services
                .CreateScope();

        BookingDbContext dbContext =
            verificationScope.ServiceProvider
                .GetRequiredService<
                    BookingDbContext>();

        Payment persistedPayment =
            await dbContext.Payments
                .AsNoTracking()
                .SingleAsync(
                    payment =>
                        payment.BookingId ==
                        bookingId,
                    cancellationToken);

        List<PaymentAttempt> persistedAttempts =
            await dbContext.PaymentAttempts
                .AsNoTracking()
                .Where(
                    attempt =>
                        attempt.PaymentId ==
                        persistedPayment.Id)
                .ToListAsync(
                    cancellationToken);

        PaymentAttempt persistedAttempt =
            Assert.Single(
                persistedAttempts);

        Assert.Equal(
            PaymentAttemptStatus.Pending,
            persistedAttempt.Status);

        Assert.Equal(
            successResult.Value.PaymentId,
            persistedPayment.Id);

        Assert.Equal(
            successResult.Value.PaymentAttemptId,
            persistedAttempt.Id);

        Assert.Equal(
            successResult.Value.ExternalReference,
            persistedAttempt.ExternalReference);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldUseBookingPriceSnapshotAsPaymentAmount()
    {
        // Arrange
        CancellationToken cancellationToken =
            TestContext.Current.CancellationToken;

        Guid bookingId =
            await CreatePendingPaymentBookingAsync();

        // Act
        using (
            IServiceScope scope =
                _factory.Services
                    .CreateScope())
        {
            ICommandExecutor<
                InitiatePaymentCommand,
                InitiatePaymentResponse> executor =
                    scope.ServiceProvider
                        .GetRequiredService<
                            ICommandExecutor<
                                InitiatePaymentCommand,
                                InitiatePaymentResponse>>();

            var command =
                new InitiatePaymentCommand(
                    bookingId,
                    "payment-price-001");

            Result<InitiatePaymentResponse> result =
                await executor.ExecuteAsync(
                    command,
                    cancellationToken);

            // Assert
            Assert.True(
                result.IsSuccess);

            Assert.Equal(
                200m,
                result.Value.Amount);

            Assert.Equal(
                "USD",
                result.Value.Currency);
        }

        using (
            IServiceScope verificationScope =
                _factory.Services
                    .CreateScope())
        {
            BookingDbContext dbContext =
                verificationScope.ServiceProvider
                    .GetRequiredService<
                        BookingDbContext>();

            Payment payment =
                await dbContext.Payments
                    .AsNoTracking()
                    .SingleAsync(
                        payment =>
                            payment.BookingId ==
                            bookingId,
                        cancellationToken);

            Assert.Equal(
                200m,
                payment.Amount.Amount);

            Assert.Equal(
                "USD",
                payment.Amount.Currency);
        }
    }

    private async Task<Guid>
        CreatePendingPaymentBookingAsync()
    {
        using IServiceScope scope =
            _factory.Services
                .CreateScope();

        BookingDbContext dbContext =
            scope.ServiceProvider
                .GetRequiredService<
                    BookingDbContext>();

        IBookingRepository bookingRepository =
            scope.ServiceProvider
                .GetRequiredService<
                    IBookingRepository>();

        IUnitOfWork unitOfWork =
            scope.ServiceProvider
                .GetRequiredService<
                    IUnitOfWork>();

        // 1. Crear y persistir primero la Property real.
        Property property =
            CreateProperty();

        dbContext.Properties.Add(
            property);

        await dbContext.SaveChangesAsync();

        // 2. La RentableUnit ahora referencia
        //    una Property que sí existe.
        RentableUnit rentableUnit =
            CreateRentableUnit(
                property.Id);

        dbContext.RentableUnits.Add(
            rentableUnit);

        await dbContext.SaveChangesAsync();

        // 3. Crear la Booking usando la unidad persistida.
        StayPeriod stayPeriod =
            StayPeriod.Create(
                new DateOnly(
                    2026,
                    10,
                    10),
                new DateOnly(
                    2026,
                    10,
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

        Result approveResult =
            booking.Approve();

        Assert.True(
            approveResult.IsSuccess);

        Assert.Equal(
            BookingStatus.PendingPayment,
            booking.Status);

        bookingRepository.Add(
            booking);

        await unitOfWork.SaveChangesAsync();

        return booking.Id;
    }

    private static Property CreateProperty()
    {
        Result<Property> result =
            Property.Create(
                $"Payment Test Property {Guid.NewGuid():N}",
                "America/El_Salvador",
                new TimeOnly(
                    15,
                    0),
                new TimeOnly(
                    11,
                    0));

        Assert.True(
            result.IsSuccess);

        return result.Value;
    }

    private static RentableUnit
        CreateRentableUnit(
            Guid propertyId)
    {
        Result<RentableUnit> result =
            RentableUnit.Create(
                propertyId,
                $"Payment Test Room {Guid.NewGuid():N}",
                RentableUnitType.Room,
                maximumCapacity: 4,
                maxBaseGuests: 2);

        Assert.True(
            result.IsSuccess);

        return result.Value;
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
            new PriceBreakdown(
                accommodationPrice,
                extraGuestPrice,
                totalPrice));
    }
}
