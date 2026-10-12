using Bookify.Services.Booking.Application.Abstractions.Persistence;
using Bookify.Services.Booking.Application.Notifications;
using Bookify.Services.Booking.Domain.Bookings;
using Bookify.Services.Booking.Domain.Shared;
using Bookify.Services.Booking.Integration.Tests.Infrastructure;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using System.Data.Common;
using DomainBooking =
    Bookify.Services.Booking.Domain.Bookings.Booking;

namespace Bookify.Services.Booking.Integration.Tests.Notifications;

[Collection(BookingApiTestFixture.Name)]
public sealed class NotificationDeliveryIdempotencyTests
{
    private readonly BookingApiFactory _factory;

    public NotificationDeliveryIdempotencyTests(
        BookingApiFactory factory)
    {
        _factory = factory
            ?? throw new ArgumentNullException(
                nameof(factory));
    }

    [Fact]
    public async Task SendAsync_WhenRetriedAcrossScopes_ShouldSendOnlyOnce()
    {
        // ARRANGE
        CancellationToken cancellationToken =
            TestContext.Current.CancellationToken;

        DomainBooking booking =
            await BookingDatabaseTestSeeder
                .SeedBookingAsync(
                    _factory,
                    status:
                        BookingStatus.PendingPayment,
                    cancellationToken:
                        cancellationToken);

        FakeBookingNotificationSender fakeSender =
            _factory.Services
                .GetRequiredService<
                    FakeBookingNotificationSender>();

        fakeSender.Reset();

        Result<BookingNotificationDispatchOutcome>
            firstResult;

        // First processing scope.
        using (IServiceScope firstScope =
            _factory.Services.CreateScope())
        {
            BookingNotificationService service =
                firstScope.ServiceProvider
                    .GetRequiredService<
                        BookingNotificationService>();

            firstResult =
                await service.SendAsync(
                    booking.Id,
                    BookingNotificationType
                        .BookingApproved,
                    cancellationToken);
        }

        // ACT
        Result<BookingNotificationDispatchOutcome>
            secondResult;

        // Simulates a later retry using an entirely new
        // DI/DbContext scope.
        using (IServiceScope secondScope =
            _factory.Services.CreateScope())
        {
            BookingNotificationService service =
                secondScope.ServiceProvider
                    .GetRequiredService<
                        BookingNotificationService>();

            secondResult =
                await service.SendAsync(
                    booking.Id,
                    BookingNotificationType
                        .BookingApproved,
                    cancellationToken);
        }

        // ASSERT
        Assert.True(
            firstResult.IsSuccess);

        Assert.Equal(
            BookingNotificationDispatchOutcome.Sent,
            firstResult.Value);

        Assert.True(
            secondResult.IsSuccess);

        Assert.Equal(
            BookingNotificationDispatchOutcome
                .AlreadyDelivered,
            secondResult.Value);

        BookingNotificationMessage sentMessage =
            Assert.Single(
                fakeSender.SentMessages);

        Assert.Equal(
            booking.Id,
            sentMessage.BookingId);

        Assert.Equal(
            BookingNotificationType.BookingApproved,
            sentMessage.Type);

        using IServiceScope verificationScope =
            _factory.Services.CreateScope();

        IDbConnectionFactory connectionFactory =
            verificationScope.ServiceProvider
                .GetRequiredService<
                    IDbConnectionFactory>();

        await using DbConnection connection =
            await connectionFactory
                .OpenConnectionAsync(
                    cancellationToken);

        long deliveryCount =
            await connection.QuerySingleAsync<long>(
                new CommandDefinition(
                    """
                    SELECT COUNT(*)
                    FROM notification_deliveries
                    WHERE booking_id = @BookingId
                      AND notification_type = @NotificationType;
                    """,
                    new
                    {
                        BookingId =
                            booking.Id,

                        NotificationType =
                            nameof(
                                BookingNotificationType
                                    .BookingApproved)
                    },
                    cancellationToken:
                        cancellationToken));

        Assert.Equal(
            1,
            deliveryCount);
    }
}
