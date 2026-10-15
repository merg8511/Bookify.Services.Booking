
using Bookify.Services.Booking.Application.Abstractions.BackgroundJobs;
using Bookify.Services.Booking.Application.Abstractions.Persistence;
using Bookify.Services.Booking.Application.Notifications;
using Bookify.Services.Booking.Domain.Bookings;
using Bookify.Services.Booking.Domain.Bookings.Events;
using Bookify.Services.Booking.Integration.Tests.Infrastructure;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using System.Data.Common;
using DomainBooking =
    Bookify.Services.Booking.Domain.Bookings.Booking;

namespace Bookify.Services.Booking.Integration.Tests.BackgroundJobs;

[Collection(BookingApiTestFixture.Name)]
[Trait("Category", "Integration")]
[Trait("Category", "Concurrency")]
public sealed class OutboxNotificationJobIntegrationTests
{
    private readonly BookingApiFactory _factory;

    public OutboxNotificationJobIntegrationTests(
        BookingApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task ExecuteBatchAsync_WhenDeliveryFails_ShouldScheduleRetryAndEventuallyDeliver()
    {
        CancellationToken cancellationToken =
            TestContext.Current.CancellationToken;

        await MarkExistingPendingOutboxAsProcessedAsync(
            cancellationToken);

        DomainBooking booking =
            await BookingDatabaseTestSeeder.SeedBookingAsync(
                _factory,
                status: BookingStatus.PendingPayment,
                cancellationToken: cancellationToken);

        FakeBookingNotificationSender sender =
            _factory.Services.GetRequiredService<
                FakeBookingNotificationSender>();

        sender.Reset();
        sender.ErrorToReturn =
            BookingNotificationErrors.DeliveryFailed(booking.Id);

        // First attempt fails.
        await ExecuteOutboxJobAsync(cancellationToken);

        OutboxRetryState state =
            await GetOutboxRetryStateAsync(
                booking.Id,
                cancellationToken);

        Assert.Equal(1, state.AttemptCount);
        Assert.Null(state.ProcessedAtUtc);
        Assert.Null(state.FailedAtUtc);
        Assert.NotNull(state.NextAttemptAtUtc);

        // Immediate polling must not retry the message.
        await ExecuteOutboxJobAsync(cancellationToken);

        OutboxRetryState unchangedState =
            await GetOutboxRetryStateAsync(
                booking.Id,
                cancellationToken);

        Assert.Equal(1, unchangedState.AttemptCount);

        // Simulate that the persisted retry deadline has passed.
        await FastForwardRetryAsync(
            booking.Id,
            cancellationToken);

        sender.Reset();

        await ExecuteOutboxJobAsync(cancellationToken);

        BookingNotificationMessage delivered =
            Assert.Single(
                sender.SentMessages,
                message =>
                    message.BookingId == booking.Id &&
                    message.Type ==
                        BookingNotificationType.BookingApproved);

        Assert.Equal(booking.Id, delivered.BookingId);

        OutboxRetryState finalState =
            await GetOutboxRetryStateAsync(
                booking.Id,
                cancellationToken);

        Assert.Equal(1, finalState.AttemptCount);
        Assert.NotNull(finalState.ProcessedAtUtc);
        Assert.Null(finalState.FailedAtUtc);
        Assert.Null(finalState.NextAttemptAtUtc);

        Assert.Equal(
            1,
            await CountDeliveryAsync(
                booking.Id,
                BookingNotificationType.BookingApproved,
                cancellationToken));
    }

    [Fact]
    public async Task ExecuteBatchAsync_AfterFiveDeliveryFailures_ShouldDeadLetterMessage()
    {
        CancellationToken cancellationToken =
            TestContext.Current.CancellationToken;

        await MarkExistingPendingOutboxAsProcessedAsync(
            cancellationToken);

        DomainBooking booking =
            await BookingDatabaseTestSeeder.SeedBookingAsync(
                _factory,
                status: BookingStatus.PendingPayment,
                cancellationToken: cancellationToken);

        FakeBookingNotificationSender sender =
            _factory.Services.GetRequiredService<
                FakeBookingNotificationSender>();

        sender.Reset();
        sender.ErrorToReturn =
            BookingNotificationErrors.DeliveryFailed(booking.Id);

        for (int attempt = 1; attempt <= 5; attempt++)
        {
            await ExecuteOutboxJobAsync(cancellationToken);

            OutboxRetryState state =
                await GetOutboxRetryStateAsync(
                    booking.Id,
                    cancellationToken);

            Assert.Equal(attempt, state.AttemptCount);

            if (attempt < 5)
            {
                Assert.Null(state.FailedAtUtc);
                Assert.NotNull(state.NextAttemptAtUtc);

                await FastForwardRetryAsync(
                    booking.Id,
                    cancellationToken);
            }
        }

        OutboxRetryState deadLetter =
            await GetOutboxRetryStateAsync(
                booking.Id,
                cancellationToken);

        Assert.Equal(5, deadLetter.AttemptCount);
        Assert.Null(deadLetter.ProcessedAtUtc);
        Assert.NotNull(deadLetter.FailedAtUtc);
        Assert.Null(deadLetter.NextAttemptAtUtc);

        // A dead-lettered message cannot be selected again.
        await ExecuteOutboxJobAsync(cancellationToken);

        OutboxRetryState afterAnotherCycle =
            await GetOutboxRetryStateAsync(
                booking.Id,
                cancellationToken);

        Assert.Equal(5, afterAnotherCycle.AttemptCount);

        Assert.Equal(
            0,
            await CountDeliveryAsync(
                booking.Id,
                BookingNotificationType.BookingApproved,
                cancellationToken));

        sender.Reset();
    }

    [Fact]
    public async Task ExecuteBatchAsync_WhenTwoWorkersCompete_ShouldDeliverNotificationOnce()
    {
        CancellationToken cancellationToken =
            TestContext.Current.CancellationToken;

        await MarkExistingPendingOutboxAsProcessedAsync(
            cancellationToken);

        DomainBooking booking =
            await BookingDatabaseTestSeeder.SeedBookingAsync(
                _factory,
                status: BookingStatus.PendingPayment,
                cancellationToken: cancellationToken);

        FakeBookingNotificationSender sender =
            _factory.Services.GetRequiredService<
                FakeBookingNotificationSender>();

        sender.Reset();

        var startGate =
            new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);

        Task<int> first =
            ExecuteOutboxJobAfterGateAsync(
                startGate.Task,
                cancellationToken);

        Task<int> second =
            ExecuteOutboxJobAfterGateAsync(
                startGate.Task,
                cancellationToken);

        startGate.SetResult(true);

        await Task.WhenAll(first, second);

        BookingNotificationMessage message =
            Assert.Single(
                sender.SentMessages,
                item =>
                    item.BookingId == booking.Id &&
                    item.Type ==
                        BookingNotificationType.BookingApproved);

        Assert.Equal(booking.Id, message.BookingId);

        Assert.Equal(
            1,
            await CountDeliveryAsync(
                booking.Id,
                BookingNotificationType.BookingApproved,
                cancellationToken));

        Assert.Equal(
            1,
            await CountProcessedOutboxAsync(
                booking.Id,
                nameof(BookingApprovedDomainEvent),
                cancellationToken));
    }

    [Fact]
    public async Task ExecuteBatchAsync_ShouldMapSupportedDomainEventsToExpectedNotifications()
    {
        CancellationToken cancellationToken =
            TestContext.Current.CancellationToken;

        await MarkExistingPendingOutboxAsProcessedAsync(
            cancellationToken);

        DomainBooking approved =
            await BookingDatabaseTestSeeder.SeedBookingAsync(
                _factory,
                status: BookingStatus.PendingPayment,
                cancellationToken: cancellationToken);

        DomainBooking paid =
            await BookingDatabaseTestSeeder.SeedBookingAsync(
                _factory,
                status: BookingStatus.Paid,
                cancellationToken: cancellationToken);

        DomainBooking cancelled =
            await BookingDatabaseTestSeeder.SeedBookingAsync(
                _factory,
                status: BookingStatus.Cancelled,
                cancellationReason:
                    BookingCancellationReason.RejectedByOwner,
                cancellationToken: cancellationToken);

        FakeBookingNotificationSender sender =
            _factory.Services.GetRequiredService<
                FakeBookingNotificationSender>();

        sender.Reset();

        await ExecuteOutboxJobAsync(cancellationToken);

        Assert.Contains(
            sender.SentMessages,
            item =>
                item.BookingId == approved.Id &&
                item.Type ==
                    BookingNotificationType.BookingApproved);

        Assert.Contains(
            sender.SentMessages,
            item =>
                item.BookingId == paid.Id &&
                item.Type ==
                    BookingNotificationType.PaymentConfirmed);

        Assert.Contains(
            sender.SentMessages,
            item =>
                item.BookingId == cancelled.Id &&
                item.Type ==
                    BookingNotificationType.BookingCancelled);
    }

    private async Task<int> ExecuteOutboxJobAsync(
        CancellationToken cancellationToken)
    {
        using IServiceScope scope =
            _factory.Services.CreateScope();

        IBackgroundJob job =
            scope.ServiceProvider.GetRequiredKeyedService<IBackgroundJob>(
                BackgroundJobClaimKeys.OutboxNotification);

        return await job.ExecuteBatchAsync(
            batchSize: 10_000,
            cancellationToken);
    }

    private async Task<int> ExecuteOutboxJobAfterGateAsync(
        Task startGate,
        CancellationToken cancellationToken)
    {
        await startGate.WaitAsync(cancellationToken);

        return await ExecuteOutboxJobAsync(
            cancellationToken);
    }

    private async Task MarkExistingPendingOutboxAsProcessedAsync(
        CancellationToken cancellationToken)
    {
        IDbConnectionFactory connectionFactory =
            _factory.Services.GetRequiredService<
                IDbConnectionFactory>();

        await using DbConnection connection =
            await connectionFactory.OpenConnectionAsync(
                cancellationToken);

        await connection.ExecuteAsync(
            new CommandDefinition(
                """
                UPDATE outbox_messages
                SET processed_at_utc = CURRENT_TIMESTAMP
                WHERE processed_at_utc IS NULL
                  AND failed_at_utc IS NULL;
                """,
                cancellationToken: cancellationToken));
    }

    private async Task FastForwardRetryAsync(
        Guid bookingId,
        CancellationToken cancellationToken)
    {
        IDbConnectionFactory connectionFactory =
            _factory.Services.GetRequiredService<
                IDbConnectionFactory>();

        await using DbConnection connection =
            await connectionFactory.OpenConnectionAsync(
                cancellationToken);

        await connection.ExecuteAsync(
            new CommandDefinition(
                """
                UPDATE outbox_messages
                SET next_attempt_at_utc =
                    CURRENT_TIMESTAMP - INTERVAL '1 second'
                WHERE event_type = @EventType
                  AND payload ->> 'bookingId' = @BookingId
                  AND processed_at_utc IS NULL
                  AND failed_at_utc IS NULL;
                """,
                new
                {
                    EventType =
                        nameof(BookingApprovedDomainEvent),
                    BookingId = bookingId.ToString()
                },
                cancellationToken: cancellationToken));
    }

    private async Task<OutboxRetryState> GetOutboxRetryStateAsync(
        Guid bookingId,
        CancellationToken cancellationToken)
    {
        IDbConnectionFactory connectionFactory =
            _factory.Services.GetRequiredService<
                IDbConnectionFactory>();

        await using DbConnection connection =
            await connectionFactory.OpenConnectionAsync(
                cancellationToken);

        return await connection.QuerySingleAsync<OutboxRetryState>(
            new CommandDefinition(
                """
                SELECT
                    attempt_count AS "AttemptCount",
                    next_attempt_at_utc AS "NextAttemptAtUtc",
                    failed_at_utc AS "FailedAtUtc",
                    processed_at_utc AS "ProcessedAtUtc"
                FROM outbox_messages
                WHERE event_type = @EventType
                  AND payload ->> 'bookingId' = @BookingId;
                """,
                new
                {
                    EventType =
                        nameof(BookingApprovedDomainEvent),
                    BookingId = bookingId.ToString()
                },
                cancellationToken: cancellationToken));
    }

    private async Task<long> CountProcessedOutboxAsync(
        Guid bookingId,
        string eventType,
        CancellationToken cancellationToken)
    {
        IDbConnectionFactory connectionFactory =
            _factory.Services.GetRequiredService<
                IDbConnectionFactory>();

        await using DbConnection connection =
            await connectionFactory.OpenConnectionAsync(
                cancellationToken);

        return await connection.ExecuteScalarAsync<long>(
            new CommandDefinition(
                """
                SELECT COUNT(*)
                FROM outbox_messages
                WHERE event_type = @EventType
                  AND payload ->> 'bookingId' = @BookingId
                  AND processed_at_utc IS NOT NULL;
                """,
                new
                {
                    EventType = eventType,
                    BookingId = bookingId.ToString()
                },
                cancellationToken: cancellationToken));
    }

    private async Task<long> CountDeliveryAsync(
        Guid bookingId,
        BookingNotificationType notificationType,
        CancellationToken cancellationToken)
    {
        IDbConnectionFactory connectionFactory =
            _factory.Services.GetRequiredService<
                IDbConnectionFactory>();

        await using DbConnection connection =
            await connectionFactory.OpenConnectionAsync(
                cancellationToken);

        return await connection.ExecuteScalarAsync<long>(
            new CommandDefinition(
                """
                SELECT COUNT(*)
                FROM notification_deliveries
                WHERE booking_id = @BookingId
                  AND notification_type = @NotificationType;
                """,
                new
                {
                    BookingId = bookingId,
                    NotificationType =
                        notificationType.ToString()
                },
                cancellationToken: cancellationToken));
    }

    private sealed class OutboxRetryState
    {
        public int AttemptCount { get; set; }

        public DateTimeOffset? NextAttemptAtUtc { get; set; }

        public DateTimeOffset? FailedAtUtc { get; set; }

        public DateTimeOffset? ProcessedAtUtc { get; set; }
    }
}
