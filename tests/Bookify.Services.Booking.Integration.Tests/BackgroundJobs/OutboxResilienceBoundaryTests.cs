
using System.Data.Common;
using Bookify.Services.Booking.Application.Abstractions.BackgroundJobs;
using Bookify.Services.Booking.Application.Abstractions.Persistence;
using Bookify.Services.Booking.Application.Notifications;
using Bookify.Services.Booking.Application.Outbox;
using Bookify.Services.Booking.Domain.Bookings;
using Bookify.Services.Booking.Domain.Bookings.Events;
using Bookify.Services.Booking.Integration.Tests.Infrastructure;
using Dapper;
using Microsoft.Extensions.DependencyInjection;

using DomainBooking =
    Bookify.Services.Booking.Domain.Bookings.Booking;

namespace Bookify.Services.Booking.Integration.Tests.BackgroundJobs;

[Collection(BookingApiTestFixture.Name)]
[Trait("Category", "Integration")]
public sealed class OutboxResilienceBoundaryTests
{
    private readonly BookingApiFactory _factory;

    public OutboxResilienceBoundaryTests(
        BookingApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task UnknownEvent_ShouldBeDeadLetteredImmediately()
    {
        // Arrange
        CancellationToken cancellationToken =
            TestContext.Current.CancellationToken;

        await ClearPendingMessagesAsync(cancellationToken);

        Guid messageId = Guid.NewGuid();

        await InsertOutboxMessageAsync(
            messageId,
            "UnsupportedDomainEvent",
            """{"bookingId":"00000000-0000-0000-0000-000000000001"}""",
            cancellationToken);

        // Act
        await ExecuteJobAsync(cancellationToken);

        // Assert
        OutboxState state =
            await GetStateAsync(
                messageId,
                cancellationToken);

        Assert.Equal(1, state.AttemptCount);
        Assert.Null(state.ProcessedAtUtc);
        Assert.NotNull(state.FailedAtUtc);
        Assert.Null(state.NextAttemptAtUtc);

        Assert.Equal(
            "Outbox.UnsupportedEventType",
            state.LastFailureCode);

        Assert.False(
            await IsEligibleAsync(
                messageId,
                cancellationToken));
    }

    [Fact]
    public async Task InvalidPayload_ShouldBeDeadLetteredImmediately()
    {
        // Arrange
        CancellationToken cancellationToken =
            TestContext.Current.CancellationToken;

        await ClearPendingMessagesAsync(cancellationToken);

        Guid messageId = Guid.NewGuid();

        await InsertOutboxMessageAsync(
            messageId,
            nameof(BookingApprovedDomainEvent),
            """{"bookingId":"invalid-guid"}""",
            cancellationToken);

        // Act
        await ExecuteJobAsync(cancellationToken);

        // Assert
        OutboxState state =
            await GetStateAsync(
                messageId,
                cancellationToken);

        Assert.Equal(1, state.AttemptCount);

        Assert.Null(state.ProcessedAtUtc);
        Assert.NotNull(state.FailedAtUtc);
        Assert.Null(state.NextAttemptAtUtc);

        Assert.Equal(
            "Outbox.InvalidPayload",
            state.LastFailureCode);

        Assert.False(
            await IsEligibleAsync(
                messageId,
                cancellationToken));
    }

    [Fact]
    public async Task DisabledDelivery_ShouldDeferWithoutConsumingAttempts()
    {
        // Arrange
        CancellationToken cancellationToken =
            TestContext.Current.CancellationToken;

        await ClearPendingMessagesAsync(cancellationToken);

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
            BookingNotificationErrors.DeliveryDisabled;

        Guid messageId =
            await GetApprovedEventMessageIdAsync(
                booking.Id,
                cancellationToken);

        // Act
        await ExecuteJobAsync(cancellationToken);

        // Assert
        OutboxState state =
            await GetStateAsync(
                messageId,
                cancellationToken);

        Assert.Equal(
            0,
            state.AttemptCount);

        Assert.Null(state.ProcessedAtUtc);
        Assert.Null(state.FailedAtUtc);

        Assert.NotNull(state.NextAttemptAtUtc);

        Assert.True(
            state.NextAttemptAtUtc.Value >
            DateTimeOffset.UtcNow);

        Assert.Null(state.LastFailureCode);

        Assert.False(
            await IsEligibleAsync(
                messageId,
                cancellationToken));

        Assert.Empty(sender.SentMessages);

        sender.Reset();
    }

    private async Task ExecuteJobAsync(
        CancellationToken cancellationToken)
    {
        using IServiceScope scope =
            _factory.Services.CreateScope();

        IBackgroundJob job =
            scope.ServiceProvider
                .GetRequiredKeyedService<IBackgroundJob>(
                    BackgroundJobClaimKeys.OutboxNotification);

        await job.ExecuteBatchAsync(
            10_000,
            cancellationToken);
    }

    private async Task InsertOutboxMessageAsync(
        Guid messageId,
        string eventType,
        string payload,
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
                INSERT INTO outbox_messages
                (
                    id,
                    event_type,
                    payload,
                    created_at_utc
                )
                VALUES
                (
                    @MessageId,
                    @EventType,
                    CAST(@Payload AS jsonb),
                    CURRENT_TIMESTAMP
                );
                """,
                new
                {
                    MessageId = messageId,
                    EventType = eventType,
                    Payload = payload
                },
                cancellationToken: cancellationToken));
    }

    private async Task ClearPendingMessagesAsync(
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

    private async Task<Guid> GetApprovedEventMessageIdAsync(
        Guid bookingId,
        CancellationToken cancellationToken)
    {
        IDbConnectionFactory connectionFactory =
            _factory.Services.GetRequiredService<
                IDbConnectionFactory>();

        await using DbConnection connection =
            await connectionFactory.OpenConnectionAsync(
                cancellationToken);

        return await connection.QuerySingleAsync<Guid>(
            new CommandDefinition(
                """
                SELECT id
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

    private async Task<OutboxState> GetStateAsync(
        Guid messageId,
        CancellationToken cancellationToken)
    {
        IDbConnectionFactory connectionFactory =
            _factory.Services.GetRequiredService<
                IDbConnectionFactory>();

        await using DbConnection connection =
            await connectionFactory.OpenConnectionAsync(
                cancellationToken);

        return await connection.QuerySingleAsync<OutboxState>(
            new CommandDefinition(
                """
                SELECT
                    attempt_count AS "AttemptCount",
                    next_attempt_at_utc AS "NextAttemptAtUtc",
                    processed_at_utc AS "ProcessedAtUtc",
                    failed_at_utc AS "FailedAtUtc",
                    last_failure_code AS "LastFailureCode"
                FROM outbox_messages
                WHERE id = @MessageId;
                """,
                new
                {
                    MessageId = messageId
                },
                cancellationToken: cancellationToken));
    }

    private async Task<bool> IsEligibleAsync(
        Guid messageId,
        CancellationToken cancellationToken)
    {
        using IServiceScope scope =
            _factory.Services.CreateScope();

        IOutboxMessageReader reader =
            scope.ServiceProvider
                .GetRequiredService<IOutboxMessageReader>();

        IReadOnlyList<PendingOutboxMessageReadModel> messages =
            await reader.GetPendingMessagesAsync(
                10_000,
                cancellationToken);

        return messages.Any(
            message => message.Id == messageId);
    }

    private sealed class OutboxState
    {
        public int AttemptCount { get; set; }

        public DateTimeOffset? NextAttemptAtUtc { get; set; }

        public DateTimeOffset? ProcessedAtUtc { get; set; }

        public DateTimeOffset? FailedAtUtc { get; set; }

        public string? LastFailureCode { get; set; }
    }
}
