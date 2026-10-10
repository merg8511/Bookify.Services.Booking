using Bookify.Services.Booking.Application.Bookings;
using Bookify.Services.Booking.Application.Bookings.ReadModels;
using Bookify.Services.Booking.Application.Notifications;
using Bookify.Services.Booking.Domain.Shared;

namespace Bookify.Services.Booking.Application.Tests.Notifications;

public sealed class BookingNotificationServiceTests
{
    [Fact]
    public async Task SendAsync_WhenBookingApproved_ShouldComposeAndSendNotification()
    {
        // ARRANGE
        CancellationToken cancellationToken =
            TestContext.Current.CancellationToken;

        Guid bookingId =
            Guid.NewGuid();

        var booking =
            new BookingDetailsReadModel
            {
                Id = bookingId,
                BookingReference = "BK-TEST-001",
                PropertyName = "Casa Bookify",
                GuestFullName = "Ada Lovelace",
                GuestEmail = "ada@example.com",
                CheckInDate =
                    new DateOnly(2026, 10, 20),
                CheckOutDate =
                    new DateOnly(2026, 10, 22),
                Status = "PendingPayment",
                PaymentDueAtUtc =
                    new DateTimeOffset(
                        2026,
                        10,
                        9,
                        23,
                        30,
                        0,
                        TimeSpan.Zero)
            };

        var sender =
            new SpyNotificationSender();

        var service =
            new BookingNotificationService(
                new StubBookingReadService(
                    booking),
                sender);

        // ACT
        Result<BookingNotificationDispatchOutcome>
            result =
                await service.SendAsync(
                    bookingId,
                    BookingNotificationType
                        .BookingApproved,
                    cancellationToken);

        // ASSERT
        Assert.True(result.IsSuccess);

        Assert.Equal(
            BookingNotificationDispatchOutcome.Sent,
            result.Value);

        BookingNotificationMessage message =
            Assert.Single(
                sender.Messages);

        Assert.Equal(
            bookingId,
            message.BookingId);

        Assert.Equal(
            BookingNotificationType.BookingApproved,
            message.Type);

        Assert.Equal(
            "ada@example.com",
            message.RecipientEmail);

        Assert.Equal(
            "Booking BK-TEST-001 approved",
            message.Subject);

        Assert.Contains(
            "Casa Bookify",
            message.Body,
            StringComparison.Ordinal);

        Assert.Contains(
            "2026-10-09 23:30 UTC",
            message.Body,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task SendAsync_WhenPaymentReminderCandidateIsNoLongerPendingPayment_ShouldSkip()
    {
        // ARRANGE
        CancellationToken cancellationToken =
            TestContext.Current.CancellationToken;

        Guid bookingId =
            Guid.NewGuid();

        var booking =
            new BookingDetailsReadModel
            {
                Id = bookingId,
                BookingReference = "BK-TEST-002",
                PropertyName = "Casa Bookify",
                GuestFullName = "Ada Lovelace",
                GuestEmail = "ada@example.com",
                CheckInDate =
                    new DateOnly(2026, 10, 20),
                CheckOutDate =
                    new DateOnly(2026, 10, 22),
                Status = "Paid",
                PaymentDueAtUtc =
                    new DateTimeOffset(
                        2026,
                        10,
                        9,
                        23,
                        30,
                        0,
                        TimeSpan.Zero)
            };

        var sender =
            new SpyNotificationSender();

        var service =
            new BookingNotificationService(
                new StubBookingReadService(
                    booking),
                sender);

        // ACT
        Result<BookingNotificationDispatchOutcome>
            result =
                await service.SendAsync(
                    bookingId,
                    BookingNotificationType
                        .PaymentDeadlineReminder,
                    cancellationToken);

        // ASSERT
        Assert.True(result.IsSuccess);

        Assert.Equal(
            BookingNotificationDispatchOutcome.Skipped,
            result.Value);

        Assert.Empty(
            sender.Messages);
    }

    private sealed class StubBookingReadService
        : IBookingReadService
    {
        private readonly BookingDetailsReadModel?
            _booking;

        public StubBookingReadService(
            BookingDetailsReadModel? booking)
        {
            _booking = booking;
        }

        public Task<BookingDetailsReadModel?>
            GetByIdAsync(
                Guid bookingId,
                CancellationToken cancellationToken = default)
        {
            cancellationToken
                .ThrowIfCancellationRequested();

            return Task.FromResult(
                _booking?.Id == bookingId
                    ? _booking
                    : null);
        }

        public Task<BookingDetailsReadModel?>
            GetByReferenceAsync(
                string bookingReference,
                CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<
            IReadOnlyList<
                BookingCalendarItemReadModel>>
            GetCalendarAsync(
                Guid propertyId,
                DateOnly rangeStart,
                DateOnly rangeEnd,
                CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class SpyNotificationSender
        : IBookingNotificationSender
    {
        public List<BookingNotificationMessage>
            Messages
        {
            get;
        } = [];

        public Task<Result> SendAsync(
            BookingNotificationMessage message,
            CancellationToken cancellationToken = default)
        {
            cancellationToken
                .ThrowIfCancellationRequested();

            Messages.Add(
                message);

            return Task.FromResult(
                Result.Success());
        }
    }
}
