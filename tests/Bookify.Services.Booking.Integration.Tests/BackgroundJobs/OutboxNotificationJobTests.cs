using System.Data.Common;
using Bookify.Services.Booking.Application.Abstractions.BackgroundJobs;
using Bookify.Services.Booking.Application.Abstractions.Persistence;
using Bookify.Services.Booking.Application.Notifications;
using Bookify.Services.Booking.Domain.Bookings;
using Bookify.Services.Booking.Integration.Tests.Infrastructure;
using Dapper;
using Microsoft.Extensions.DependencyInjection;

using DomainBooking =
    Bookify.Services.Booking.Domain.Bookings.Booking;

namespace Bookify.Services.Booking.Integration.Tests.BackgroundJobs;

[Collection(BookingApiTestFixture.Name)]
[Trait("Category", "Integration")]
public sealed class OutboxNotificationJobTests
{
    private readonly BookingApiFactory _factory;

    public OutboxNotificationJobTests(
        BookingApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task ExecuteBatchAsync_WhenBookingIsCancelled_ShouldSendCancelledNotification()
    {
        // ARRANGE
        CancellationToken cancellationToken =
            TestContext.Current.CancellationToken;

        await MarkExistingPendingOutboxAsProcessedAsync(
            cancellationToken);

        DomainBooking booking =
            await BookingDatabaseTestSeeder
                .SeedBookingAsync(
                    _factory,
                    status:
                        BookingStatus.Cancelled,
                    cancellationReason:
                        BookingCancellationReason.RejectedByOwner,
                    cancellationToken:
                        cancellationToken);

        FakeBookingNotificationSender sender =
            _factory.Services
                .GetRequiredService<
                    FakeBookingNotificationSender>();

        sender.Reset();

        using IServiceScope scope =
            _factory.Services.CreateScope();

        IBackgroundJob job =
            scope.ServiceProvider
                .GetRequiredKeyedService<IBackgroundJob>(
                    BackgroundJobClaimKeys
                        .OutboxNotification);

        // ACT
        await job.ExecuteBatchAsync(
            batchSize: 100,
            cancellationToken);

        // ASSERT
        BookingNotificationMessage message =
            Assert.Single(
                sender.SentMessages,
                message =>
                    message.BookingId ==
                        booking.Id);

        Assert.Equal(
            BookingNotificationType.BookingCancelled,
            message.Type);
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
                WHERE processed_at_utc IS NULL
                  AND failed_at_utc IS NULL;
                """,
                cancellationToken:
                    cancellationToken));
    }
}
