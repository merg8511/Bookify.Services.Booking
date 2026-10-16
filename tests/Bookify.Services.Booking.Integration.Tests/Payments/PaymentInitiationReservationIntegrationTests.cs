
using System.Data.Common;
using Bookify.Services.Booking.Application.Abstractions.Payments;
using Bookify.Services.Booking.Application.Abstractions.Persistence;
using Bookify.Services.Booking.Application.Abstractions.Persistence.Repositories;
using Bookify.Services.Booking.Application.Abstractions.Time;
using Bookify.Services.Booking.Application.Payments.Initiate;
using Bookify.Services.Booking.Domain.Bookings;
using Bookify.Services.Booking.Domain.Shared;
using Bookify.Services.Booking.Integration.Tests.Infrastructure;
using Dapper;
using Microsoft.Extensions.DependencyInjection;

using DomainBooking =
    Bookify.Services.Booking.Domain.Bookings.Booking;

namespace Bookify.Services.Booking.Integration.Tests.Payments;

[Collection(BookingApiTestFixture.Name)]
[Trait("Category", "Integration")]
public sealed class PaymentInitiationReservationIntegrationTests
{
    private readonly BookingApiFactory _factory;

    public PaymentInitiationReservationIntegrationTests(
        BookingApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task ProviderTimeout_ShouldPersistReservationAndBlockDifferentKey()
    {
        CancellationToken token =
            TestContext.Current.CancellationToken;

        DomainBooking booking =
            await BookingDatabaseTestSeeder.SeedBookingAsync(
                _factory,
                status: BookingStatus.PendingPayment,
                cancellationToken: token);

        var gateway = new ReservationCheckingGateway(
            _factory.Services.GetRequiredService<
                IDbConnectionFactory>())
        {
            FailFirstCall = true
        };

        Result<InitiatePaymentResponse> first =
            await ExecuteAsync(
                booking.Id,
                "operation-original",
                gateway,
                token);

        Assert.True(first.IsFailure);
        Assert.Equal(
            PaymentGatewayErrors.ProviderTimeout,
            first.Error);

        Assert.Equal(1, gateway.CallCount);

        // The reservation must be durable even though
        // no PaymentAttempt was created.
        PaymentState state =
            await GetPaymentStateAsync(booking.Id, token);

        Assert.NotNull(state.PendingInitiationKey);
        Assert.NotNull(state.PendingInitiationCreatedAtUtc);
        Assert.Equal(0, state.AttemptCount);

        // A different key cannot create another PaymentIntent.
        Result<InitiatePaymentResponse> second =
            await ExecuteAsync(
                booking.Id,
                "different-operation",
                gateway,
                token);

        Assert.True(second.IsFailure);

        Assert.Equal(
            InitiatePaymentErrors.PaymentOutcomeUncertain,
            second.Error);

        Assert.Equal(1, gateway.CallCount);

        // Recover using the SAME original operation.
        Result<InitiatePaymentResponse> recovered =
            await ExecuteAsync(
                booking.Id,
                "operation-original",
                gateway,
                token);

        Assert.True(recovered.IsSuccess);
        Assert.Equal(2, gateway.CallCount);

        Assert.Equal(
            gateway.ObservedKeys[0],
            gateway.ObservedKeys[1]);

        PaymentState recoveredState =
            await GetPaymentStateAsync(booking.Id, token);

        Assert.Equal(1, recoveredState.AttemptCount);
        Assert.Null(recoveredState.PendingInitiationKey);
        Assert.Null(
            recoveredState.PendingInitiationCreatedAtUtc);

        Assert.Equal(
            "pi_reservation_test",
            recovered.Value.ExternalReference);
    }

    [Fact]
    public async Task ProviderCrash_ShouldLeaveCommittedReservation()
    {
        CancellationToken token =
            TestContext.Current.CancellationToken;

        DomainBooking booking =
            await BookingDatabaseTestSeeder.SeedBookingAsync(
                _factory,
                status: BookingStatus.PendingPayment,
                cancellationToken: token);

        var gateway = new ReservationCheckingGateway(
            _factory.Services.GetRequiredService<
                IDbConnectionFactory>())
        {
            ThrowOnFirstCall = true
        };

        // Simulate a failure after the gateway has been reached.
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => ExecuteAsync(
                booking.Id,
                "crashed-operation",
                gateway,
                token));

        PaymentState state =
            await GetPaymentStateAsync(booking.Id, token);

        Assert.NotNull(state.PendingInitiationKey);
        Assert.Equal(0, state.AttemptCount);
        Assert.Equal(1, gateway.CallCount);

        Result<InitiatePaymentResponse> anotherOperation =
            await ExecuteAsync(
                booking.Id,
                "new-key-after-crash",
                gateway,
                token);

        Assert.True(anotherOperation.IsFailure);

        Assert.Equal(
            InitiatePaymentErrors.PaymentOutcomeUncertain,
            anotherOperation.Error);

        Assert.Equal(1, gateway.CallCount);
    }

    [Fact]
    public async Task ExpiredReservation_ShouldNotRepeatProviderCreation()
    {
        CancellationToken token =
            TestContext.Current.CancellationToken;

        DomainBooking booking =
            await BookingDatabaseTestSeeder.SeedBookingAsync(
                _factory,
                status: BookingStatus.PendingPayment,
                cancellationToken: token);

        var gateway = new ReservationCheckingGateway(
            _factory.Services.GetRequiredService<
                IDbConnectionFactory>())
        {
            FailFirstCall = true
        };

        await ExecuteAsync(
            booking.Id,
            "expired-operation",
            gateway,
            token);

        await using (
            DbConnection connection =
                await _factory.Services
                    .GetRequiredService<IDbConnectionFactory>()
                    .OpenConnectionAsync(token))
        {
            await connection.ExecuteAsync(
                new CommandDefinition(
                    """
                    UPDATE payments
                    SET pending_initiation_created_at_utc =
                        CURRENT_TIMESTAMP - INTERVAL '25 hours'
                    WHERE booking_id = @BookingId;
                    """,
                    new
                    {
                        BookingId = booking.Id
                    },
                    cancellationToken: token));
        }

        Result<InitiatePaymentResponse> result =
            await ExecuteAsync(
                booking.Id,
                "expired-operation",
                gateway,
                token);

        Assert.True(result.IsFailure);

        Assert.Equal(
            InitiatePaymentErrors.SessionRecoveryWindowExpired,
            result.Error);

        Assert.Equal(1, gateway.CallCount);
    }

    private async Task<Result<InitiatePaymentResponse>>
        ExecuteAsync(
            Guid bookingId,
            string idempotencyKey,
            IPaymentGateway gateway,
            CancellationToken cancellationToken)
    {
        await using AsyncServiceScope scope =
            _factory.Services.CreateAsyncScope();

        IServiceProvider services = scope.ServiceProvider;

        var handler = new InitiatePaymentCommandHandler(
            services.GetRequiredService<IBookingRepository>(),
            services.GetRequiredService<IPaymentRepository>(),
            gateway,
            services.GetRequiredService<IUnitOfWork>(),
            services.GetRequiredService<ITransactionManager>(),
            services.GetRequiredService<IPaymentInitiationLock>(),
            services.GetRequiredService<IClock>());

        return await handler.HandleAsync(
            new InitiatePaymentCommand(
                bookingId,
                idempotencyKey),
            cancellationToken);
    }

    private async Task<PaymentState> GetPaymentStateAsync(
        Guid bookingId,
        CancellationToken cancellationToken)
    {
        IDbConnectionFactory connectionFactory =
            _factory.Services
                .GetRequiredService<IDbConnectionFactory>();

        await using DbConnection connection =
            await connectionFactory.OpenConnectionAsync(
                cancellationToken);

        return await connection.QuerySingleAsync<PaymentState>(
            new CommandDefinition(
                """
                SELECT
                    p.pending_initiation_key AS "PendingInitiationKey",
                    p.pending_initiation_created_at_utc
                        AS "PendingInitiationCreatedAtUtc",
                    (
                        SELECT COUNT(*)::integer
                        FROM payment_attempts AS a
                        WHERE a.payment_id = p.id
                    ) AS "AttemptCount"
                FROM payments AS p
                WHERE p.booking_id = @BookingId;
                """,
                new
                {
                    BookingId = bookingId
                },
                cancellationToken: cancellationToken));
    }

    private sealed class PaymentState
    {
        public string? PendingInitiationKey { get; set; }
        public DateTimeOffset? PendingInitiationCreatedAtUtc
        {
            get;
            set;
        }

        public int AttemptCount { get; set; }
    }

    private sealed class ReservationCheckingGateway :
        IPaymentGateway
    {
        private readonly IDbConnectionFactory _connectionFactory;

        public ReservationCheckingGateway(
            IDbConnectionFactory connectionFactory)
        {
            _connectionFactory = connectionFactory;
        }

        public bool FailFirstCall { get; set; }
        public bool ThrowOnFirstCall { get; set; }

        public int CallCount { get; private set; }

        public List<string> ObservedKeys { get; } = [];

        public async Task<Result<CreatePaymentAttemptResponse>>
            CreatePaymentAttemptAsync(
                CreatePaymentAttemptRequest request,
                CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            CallCount++;
            ObservedKeys.Add(request.IdempotencyKey);

            // A SEPARATE connection must see the reservation.
            // If the preparation transaction was not committed,
            // this assertion fails.
            await using DbConnection connection =
                await _connectionFactory.OpenConnectionAsync(
                    cancellationToken);

            string? persistedKey =
                await connection.QuerySingleOrDefaultAsync<string>(
                    new CommandDefinition(
                        """
                        SELECT pending_initiation_key
                        FROM payments
                        WHERE booking_id = @BookingId;
                        """,
                        new
                        {
                            request.BookingId
                        },
                        cancellationToken: cancellationToken));

            Assert.Equal(
                request.IdempotencyKey,
                persistedKey);

            if (CallCount == 1 && ThrowOnFirstCall)
            {
                throw new InvalidOperationException(
                    "Simulated provider crash.");
            }

            if (CallCount == 1 && FailFirstCall)
            {
                return Result<CreatePaymentAttemptResponse>
                    .Failure(
                        PaymentGatewayErrors.ProviderTimeout);
            }

            return Result<CreatePaymentAttemptResponse>.Success(
                new CreatePaymentAttemptResponse(
                    "pi_reservation_test",
                    PaymentGatewayStatus.Pending,
                    "pi_reservation_test_secret"));
        }

        public Task<Result<PaymentGatewayResponse>>
            GetPaymentStatusAsync(
                string externalReference,
                CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<Result<PaymentGatewayResponse>>
            CancelPaymentAsync(
                string externalReference,
                CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }
}
