using System.Data.Common;
using Bookify.Services.Booking.Application.Abstractions.BackgroundJobs;
using Bookify.Services.Booking.Application.Abstractions.Persistence;
using Bookify.Services.Booking.Application.BackgroundJobs.Notifications;
using Bookify.Services.Booking.Application.Notifications;
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
    public async Task ExecuteBatchAsync_WhenDeliveryFails_ShouldLeaveOutboxPendingAndRetrySuccessfully()
    {
        CancellationToken cancellationToken =
            TestContext.Current.CancellationToken;

        await MarkExistingPendingOutboxAsProcessedAsync(
            cancellationToken);

        DomainBooking booking =
            await BookingDatabaseTestSeeder
                .SeedBookingAsync(
                    _factory,
                    status:
                        BookingStatus.PendingPayment,
                    cancellationToken:
                        cancellationToken);

        FakeBookingNotificationSender sender =
            _factory.Services
                .GetRequiredService<
                    FakeBookingNotificationSender>();

        sender.Reset();

        sender.ErrorToReturn =
            BookingNotificationErrors
                .DeliveryFailed(
                    booking.Id);

        await ExecuteOutboxJobAsync(
            cancellationToken);

        long processedAfterFailure =
            await CountProcessedOutboxAsync(
                booking.Id,
                nameof(
                    BookingApprovedDomainEvent),
                cancellationToken);

        long deliveryAfterFailure =
            await CountDeliveryAsync(
                booking.Id,
                BookingNotificationType
                    .BookingApproved,
                cancellationToken);

        Assert.Equal(
            0,
            processedAfterFailure);

        Assert.Equal(
            0,
            deliveryAfterFailure);

        sender.Reset();

        await ExecuteOutboxJobAsync(
            cancellationToken);

        BookingNotificationMessage message =
            Assert.Single(
                sender.SentMessages,
                message =>
                    message.BookingId ==
                        booking.Id &&
                    message.Type ==
                        BookingNotificationType
                            .BookingApproved);

        Assert.Equal(
            BookingNotificationType.BookingApproved,
            message.Type);

        long processedAfterRetry =
            await CountProcessedOutboxAsync(
                booking.Id,
                nameof(
                    BookingApprovedDomainEvent),
                cancellationToken);

        long deliveryAfterRetry =
            await CountDeliveryAsync(
                booking.Id,
                BookingNotificationType
                    .BookingApproved,
                cancellationToken);

        Assert.Equal(
            1,
            processedAfterRetry);

        Assert.Equal(
            1,
            deliveryAfterRetry);
    }

    [Fact]
    public async Task ExecuteBatchAsync_WhenTwoWorkersCompete_ShouldDeliverNotificationOnce()
    {
        CancellationToken cancellationToken =
            TestContext.Current.CancellationToken;

        await MarkExistingPendingOutboxAsProcessedAsync(
            cancellationToken);

        DomainBooking booking =
            await BookingDatabaseTestSeeder
                .SeedBookingAsync(
                    _factory,
                    status:
                        BookingStatus.PendingPayment,
                    cancellationToken:
                        cancellationToken);

        FakeBookingNotificationSender sender =
            _factory.Services
                .GetRequiredService<
                    FakeBookingNotificationSender>();

        sender.Reset();

        var startGate =
            new TaskCompletionSource<bool>(
                TaskCreationOptions
                    .RunContinuationsAsynchronously);

        Task<int> first =
            ExecuteOutboxJobAfterGateAsync(
                startGate.Task,
                cancellationToken);

        Task<int> second =
            ExecuteOutboxJobAfterGateAsync(
                startGate.Task,
                cancellationToken);

        startGate.SetResult(
            true);

        await Task.WhenAll(
            first,
            second);

        BookingNotificationMessage message =
            Assert.Single(
                sender.SentMessages,
                message =>
                    message.BookingId ==
                        booking.Id &&
                    message.Type ==
                        BookingNotificationType
                            .BookingApproved);

        Assert.Equal(
            booking.Id,
            message.BookingId);

        Assert.Equal(
            1,
            await CountDeliveryAsync(
                booking.Id,
                BookingNotificationType
                    .BookingApproved,
                cancellationToken));

        Assert.Equal(
            1,
            await CountProcessedOutboxAsync(
                booking.Id,
                nameof(
                    BookingApprovedDomainEvent),
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
            await BookingDatabaseTestSeeder
                .SeedBookingAsync(
                    _factory,
                    status:
                        BookingStatus.PendingPayment,
                    cancellationToken:
                        cancellationToken);

        DomainBooking paid =
            await BookingDatabaseTestSeeder
                .SeedBookingAsync(
                    _factory,
                    status:
                        BookingStatus.Paid,
                    cancellationToken:
                        cancellationToken);

        DomainBooking cancelled =
            await BookingDatabaseTestSeeder
                .SeedBookingAsync(
                    _factory,
                    status:
                        BookingStatus.Cancelled,
                    cancellationReason:
                        BookingCancellationReason
                            .RejectedByOwner,
                    cancellationToken:
                        cancellationToken);

        FakeBookingNotificationSender sender =
            _factory.Services
                .GetRequiredService<
                    FakeBookingNotificationSender>();

        sender.Reset();

        await ExecuteOutboxJobAsync(
            cancellationToken);

        Assert.Contains(
            sender.SentMessages,
            message =>
                message.BookingId ==
                    approved.Id &&
                message.Type ==
                    BookingNotificationType
                        .BookingApproved);

        Assert.Contains(
            sender.SentMessages,
            message =>
                message.BookingId ==
                    paid.Id &&
                message.Type ==
                    BookingNotificationType
                        .PaymentConfirmed);

        Assert.Contains(
            sender.SentMessages,
            message =>
                message.BookingId ==
                    cancelled.Id &&
                message.Type ==
                    BookingNotificationType
                        .BookingCancelled);
    }

    private async Task<int> ExecuteOutboxJobAsync(
        CancellationToken cancellationToken)
    {
        using IServiceScope scope =
            _factory.Services.CreateScope();

        OutboxNotificationJob job =
            scope.ServiceProvider
                .GetServices<IBackgroundJob>()
                .OfType<OutboxNotificationJob>()
                .Single();

        return await job.ExecuteBatchAsync(
            batchSize: 10_000,
            cancellationToken);
    }

    private async Task<int> ExecuteOutboxJobAfterGateAsync(
        Task startGate,
        CancellationToken cancellationToken)
    {
        await startGate.WaitAsync(
            cancellationToken);

        return await ExecuteOutboxJobAsync(
            cancellationToken);
    }

    private async Task MarkExistingPendingOutboxAsProcessedAsync(
        CancellationToken cancellationToken)
    {
        IDbConnectionFactory connectionFactory =
            _factory.Services
                .GetRequiredService<
                    IDbConnectionFactory>();

        await using DbConnection connection =
            await connectionFactory
                .OpenConnectionAsync(
                    cancellationToken);

        await connection.ExecuteAsync(
            new CommandDefinition(
                """
                UPDATE outbox_messages
                SET processed_at_utc = CURRENT_TIMESTAMP
                WHERE processed_at_utc IS NULL;
                """,
                cancellationToken:
                    cancellationToken));
    }

    private async Task<long> CountProcessedOutboxAsync(
        Guid bookingId,
        string eventType,
        CancellationToken cancellationToken)
    {
        IDbConnectionFactory connectionFactory =
            _factory.Services
                .GetRequiredService<
                    IDbConnectionFactory>();

        await using DbConnection connection =
            await connectionFactory
                .OpenConnectionAsync(
                    cancellationToken);

        return await connection
            .ExecuteScalarAsync<long>(
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
                        EventType =
                            eventType,

                        BookingId =
                            bookingId.ToString()
                    },
                    cancellationToken:
                        cancellationToken));
    }

    private async Task<long> CountDeliveryAsync(
        Guid bookingId,
        BookingNotificationType notificationType,
        CancellationToken cancellationToken)
    {
        IDbConnectionFactory connectionFactory =
            _factory.Services
                .GetRequiredService<
                    IDbConnectionFactory>();

        await using DbConnection connection =
            await connectionFactory
                .OpenConnectionAsync(
                    cancellationToken);

        return await connection
            .ExecuteScalarAsync<long>(
                new CommandDefinition(
                    """
                    SELECT COUNT(*)
                    FROM notification_deliveries
                    WHERE booking_id = @BookingId
                      AND notification_type =
                          @NotificationType;
                    """,
                    new
                    {
                        BookingId =
                            bookingId,

                        NotificationType =
                            notificationType.ToString()
                    },
                    cancellationToken:
                        cancellationToken));
    }
}
