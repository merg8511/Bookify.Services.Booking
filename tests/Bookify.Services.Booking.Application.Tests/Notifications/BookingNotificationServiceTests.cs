using Bookify.Services.Booking.Application.Abstractions.Time;
using Bookify.Services.Booking.Application.Bookings;
using Bookify.Services.Booking.Application.Bookings.ReadModels;
using Bookify.Services.Booking.Application.Notifications;
using Bookify.Services.Booking.Domain.Shared;

namespace Bookify.Services.Booking.Application.Tests.Notifications;

public sealed class BookingNotificationServiceTests
{
    private static readonly DateTimeOffset UtcNow =
        new(
            2026,
            10,
            10,
            18,
            0,
            0,
            TimeSpan.Zero);

    [Fact]
    public async Task SendAsync_WhenBookingApproved_ShouldComposeSendAndRecordDelivery()
    {
        // ARRANGE
        CancellationToken cancellationToken =
            TestContext.Current.CancellationToken;

        Guid bookingId =
            Guid.NewGuid();

        BookingDetailsReadModel booking =
            CreateBooking(
                bookingId,
                status: "PendingPayment");

        var sender =
            new SpyNotificationSender();

        var deliveryStore =
            new StubNotificationDeliveryStore();

        var service =
            CreateService(
                booking,
                sender,
                deliveryStore);

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

        Assert.True(
            deliveryStore.Contains(
                bookingId,
                BookingNotificationType
                    .BookingApproved));

        Assert.Equal(
            1,
            deliveryStore.RecordCallCount);
    }

    [Fact]
    public async Task SendAsync_WhenSameNotificationWasAlreadyDelivered_ShouldNotSendAgain()
    {
        // ARRANGE
        CancellationToken cancellationToken =
            TestContext.Current.CancellationToken;

        Guid bookingId =
            Guid.NewGuid();

        BookingDetailsReadModel booking =
            CreateBooking(
                bookingId,
                status: "PendingPayment");

        var sender =
            new SpyNotificationSender();

        var deliveryStore =
            new StubNotificationDeliveryStore();

        var service =
            CreateService(
                booking,
                sender,
                deliveryStore);

        Result<BookingNotificationDispatchOutcome>
            firstResult =
                await service.SendAsync(
                    bookingId,
                    BookingNotificationType
                        .BookingApproved,
                    cancellationToken);

        // ACT
        Result<BookingNotificationDispatchOutcome>
            secondResult =
                await service.SendAsync(
                    bookingId,
                    BookingNotificationType
                        .BookingApproved,
                    cancellationToken);

        // ASSERT
        Assert.True(firstResult.IsSuccess);

        Assert.Equal(
            BookingNotificationDispatchOutcome.Sent,
            firstResult.Value);

        Assert.True(secondResult.IsSuccess);

        Assert.Equal(
            BookingNotificationDispatchOutcome
                .AlreadyDelivered,
            secondResult.Value);

        Assert.Single(
            sender.Messages);

        Assert.Equal(
            1,
            deliveryStore.RecordCallCount);
    }

    [Fact]
    public async Task SendAsync_WhenSameBookingUsesDifferentNotificationType_ShouldSendBoth()
    {
        // ARRANGE
        CancellationToken cancellationToken =
            TestContext.Current.CancellationToken;

        Guid bookingId =
            Guid.NewGuid();

        BookingDetailsReadModel booking =
            CreateBooking(
                bookingId,
                status: "Paid");

        var sender =
            new SpyNotificationSender();

        var deliveryStore =
            new StubNotificationDeliveryStore();

        var service =
            CreateService(
                booking,
                sender,
                deliveryStore);

        // ACT
        Result<BookingNotificationDispatchOutcome>
            approvedResult =
                await service.SendAsync(
                    bookingId,
                    BookingNotificationType
                        .BookingApproved,
                    cancellationToken);

        Result<BookingNotificationDispatchOutcome>
            paymentResult =
                await service.SendAsync(
                    bookingId,
                    BookingNotificationType
                        .PaymentConfirmed,
                    cancellationToken);

        // ASSERT
        Assert.True(
            approvedResult.IsSuccess);

        Assert.True(
            paymentResult.IsSuccess);

        Assert.Equal(
            2,
            sender.Messages.Count);

        Assert.True(
            deliveryStore.Contains(
                bookingId,
                BookingNotificationType
                    .BookingApproved));

        Assert.True(
            deliveryStore.Contains(
                bookingId,
                BookingNotificationType
                    .PaymentConfirmed));

        Assert.Equal(
            2,
            deliveryStore.RecordCallCount);
    }

    [Fact]
    public async Task SendAsync_WhenSenderFails_ShouldNotRecordDelivery()
    {
        // ARRANGE
        CancellationToken cancellationToken =
            TestContext.Current.CancellationToken;

        Guid bookingId =
            Guid.NewGuid();

        BookingDetailsReadModel booking =
            CreateBooking(
                bookingId,
                status: "PendingPayment");

        var sender =
            new SpyNotificationSender
            {
                ErrorToReturn =
                    BookingNotificationErrors
                        .DeliveryFailed(
                            bookingId)
            };

        var deliveryStore =
            new StubNotificationDeliveryStore();

        var service =
            CreateService(
                booking,
                sender,
                deliveryStore);

        // ACT
        Result<BookingNotificationDispatchOutcome>
            result =
                await service.SendAsync(
                    bookingId,
                    BookingNotificationType
                        .BookingApproved,
                    cancellationToken);

        // ASSERT
        Assert.True(result.IsFailure);

        Assert.Equal(
            BookingNotificationErrors
                .DeliveryFailed(
                    bookingId),
            result.Error);

        Assert.False(
            deliveryStore.Contains(
                bookingId,
                BookingNotificationType
                    .BookingApproved));

        Assert.Equal(
            0,
            deliveryStore.RecordCallCount);
    }

    [Fact]
    public async Task SendAsync_WhenReminderCandidateIsStale_ShouldSkipWithoutRecordingDelivery()
    {
        // ARRANGE
        CancellationToken cancellationToken =
            TestContext.Current.CancellationToken;

        Guid bookingId =
            Guid.NewGuid();

        BookingDetailsReadModel booking =
            CreateBooking(
                bookingId,
                status: "Paid");

        var sender =
            new SpyNotificationSender();

        var deliveryStore =
            new StubNotificationDeliveryStore();

        var service =
            CreateService(
                booking,
                sender,
                deliveryStore);

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

        Assert.False(
            deliveryStore.Contains(
                bookingId,
                BookingNotificationType
                    .PaymentDeadlineReminder));

        Assert.Equal(
            0,
            deliveryStore.RecordCallCount);
    }

    private static BookingNotificationService
        CreateService(
            BookingDetailsReadModel booking,
            IBookingNotificationSender sender,
            INotificationDeliveryStore deliveryStore)
    {
        return new BookingNotificationService(
            new StubBookingReadService(
                booking),
            sender,
            deliveryStore,
            new StubClock(UtcNow));
    }

    private static BookingDetailsReadModel
        CreateBooking(
            Guid bookingId,
            string status)
    {
        return new BookingDetailsReadModel
        {
            Id = bookingId,
            BookingReference =
                "BK-TEST-001",

            PropertyName =
                "Casa Bookify",

            GuestFullName =
                "Ada Lovelace",

            GuestEmail =
                "ada@example.com",

            CheckInDate =
                new DateOnly(
                    2026,
                    10,
                    20),

            CheckOutDate =
                new DateOnly(
                    2026,
                    10,
                    22),

            Status =
                status,

            PaymentDueAtUtc =
                new DateTimeOffset(
                    2026,
                    10,
                    10,
                    23,
                    30,
                    0,
                    TimeSpan.Zero)
        };
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

        public Error? ErrorToReturn
        {
            get;
            set;
        }

        public Task<Result> SendAsync(
            BookingNotificationMessage message,
            CancellationToken cancellationToken = default)
        {
            cancellationToken
                .ThrowIfCancellationRequested();

            Messages.Add(
                message);

            if (ErrorToReturn is not null)
            {
                return Task.FromResult(
                    Result.Failure(
                        ErrorToReturn));
            }

            return Task.FromResult(
                Result.Success());
        }
    }

    private sealed class StubNotificationDeliveryStore
        : INotificationDeliveryStore
    {
        private readonly HashSet<
            (Guid BookingId, BookingNotificationType Type)>
            _deliveries = [];

        public int RecordCallCount
        {
            get;
            private set;
        }

        public Task<bool> HasBeenDeliveredAsync(
            Guid bookingId,
            BookingNotificationType notificationType,
            CancellationToken cancellationToken = default)
        {
            cancellationToken
                .ThrowIfCancellationRequested();

            return Task.FromResult(
                Contains(
                    bookingId,
                    notificationType));
        }

        public Task<bool> TryRecordDeliveredAsync(
            Guid bookingId,
            BookingNotificationType notificationType,
            DateTimeOffset deliveredAtUtc,
            CancellationToken cancellationToken = default)
        {
            cancellationToken
                .ThrowIfCancellationRequested();

            RecordCallCount++;

            bool added =
                _deliveries.Add(
                    (
                        bookingId,
                        notificationType
                    ));

            return Task.FromResult(
                added);
        }

        public bool Contains(
            Guid bookingId,
            BookingNotificationType notificationType)
        {
            return _deliveries.Contains(
                (
                    bookingId,
                    notificationType
                ));
        }
    }

    private sealed class StubClock
        : IClock
    {
        public StubClock(
            DateTimeOffset utcNow)
        {
            UtcNow = utcNow;
        }

        public DateTimeOffset UtcNow
        {
            get;
        }
    }
}
