using Bookify.Services.Booking.Application.Bookings;
using Bookify.Services.Booking.Application.Bookings.ReadModels;
using Bookify.Services.Booking.Application.Notifications;
using Bookify.Services.Booking.Application.Tests.Infrastructure;
using Bookify.Services.Booking.Domain.Bookings;
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

    [Theory]
    [InlineData(
        BookingNotificationType.BookingApproved,
        "PendingPayment",
        "approved",
        "Payment deadline")]
    [InlineData(
        BookingNotificationType.PaymentConfirmed,
        "Paid",
        "Payment confirmed",
        "has been confirmed")]
    [InlineData(
        BookingNotificationType.BookingCancelled,
        "Cancelled",
        "cancelled",
        "payment deadline expired")]
    [InlineData(
        BookingNotificationType.PaymentDeadlineReminder,
        "PendingPayment",
        "Payment reminder",
        "still pending")]
    [InlineData(
        BookingNotificationType.UpcomingCheckInReminder,
        "Paid",
        "Upcoming check-in",
        "is approaching")]
    public async Task SendAsync_WhenNotificationIsApplicable_ShouldComposeSendAndRecordDelivery(
        BookingNotificationType notificationType,
        string status,
        string expectedSubjectFragment,
        string expectedBodyFragment)
    {
        CancellationToken cancellationToken =
            TestContext.Current.CancellationToken;

        Guid bookingId =
            Guid.NewGuid();

        BookingDetailsReadModel booking =
            CreateBooking(
                bookingId,
                status);

        var sender =
            new SpyNotificationSender();

        var deliveryStore =
            new StubNotificationDeliveryStore();

        var service =
            CreateService(
                booking,
                sender,
                deliveryStore);

        Result<BookingNotificationDispatchOutcome> result =
            await service.SendAsync(
                bookingId,
                notificationType,
                cancellationToken);

        Assert.True(
            result.IsSuccess);

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
            notificationType,
            message.Type);

        Assert.Equal(
            "ada@example.com",
            message.RecipientEmail);

        Assert.Contains(
            expectedSubjectFragment,
            message.Subject,
            StringComparison.OrdinalIgnoreCase);

        Assert.Contains(
            expectedBodyFragment,
            message.Body,
            StringComparison.OrdinalIgnoreCase);

        Assert.True(
            deliveryStore.Contains(
                bookingId,
                notificationType));
    }

    [Fact]
    public async Task SendAsync_WhenNotificationWasAlreadyDelivered_ShouldReturnAlreadyDeliveredWithoutReadingBookingOrSending()
    {
        CancellationToken cancellationToken =
            TestContext.Current.CancellationToken;

        Guid bookingId =
            Guid.NewGuid();

        BookingDetailsReadModel booking =
            CreateBooking(
                bookingId,
                "PendingPayment");

        var readService =
            new StubBookingReadService(
                booking);

        var sender =
            new SpyNotificationSender();

        var deliveryStore =
            new StubNotificationDeliveryStore();

        deliveryStore.Seed(
            bookingId,
            BookingNotificationType
                .BookingApproved);

        var service =
            new BookingNotificationService(
                readService,
                sender,
                deliveryStore,
                new FixedClock(UtcNow));

        Result<BookingNotificationDispatchOutcome> result =
            await service.SendAsync(
                bookingId,
                BookingNotificationType
                    .BookingApproved,
                cancellationToken);

        Assert.True(
            result.IsSuccess);

        Assert.Equal(
            BookingNotificationDispatchOutcome
                .AlreadyDelivered,
            result.Value);

        Assert.Equal(
            0,
            readService.GetByIdCallCount);

        Assert.Empty(
            sender.Messages);
    }

    [Fact]
    public async Task SendAsync_WhenSameBookingHasDifferentNotificationTypes_ShouldDeliverEachTypeOnce()
    {
        CancellationToken cancellationToken =
            TestContext.Current.CancellationToken;

        Guid bookingId =
            Guid.NewGuid();

        BookingDetailsReadModel booking =
            CreateBooking(
                bookingId,
                "Paid");

        var sender =
            new SpyNotificationSender();

        var deliveryStore =
            new StubNotificationDeliveryStore();

        var service =
            CreateService(
                booking,
                sender,
                deliveryStore);

        Result<BookingNotificationDispatchOutcome> first =
            await service.SendAsync(
                bookingId,
                BookingNotificationType
                    .BookingApproved,
                cancellationToken);

        Result<BookingNotificationDispatchOutcome> second =
            await service.SendAsync(
                bookingId,
                BookingNotificationType
                    .PaymentConfirmed,
                cancellationToken);

        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);

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
    }

    [Fact]
    public async Task SendAsync_WhenSenderFails_ShouldNotRecordDelivery()
    {
        CancellationToken cancellationToken =
            TestContext.Current.CancellationToken;

        Guid bookingId =
            Guid.NewGuid();

        BookingDetailsReadModel booking =
            CreateBooking(
                bookingId,
                "PendingPayment");

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

        Result<BookingNotificationDispatchOutcome> result =
            await service.SendAsync(
                bookingId,
                BookingNotificationType
                    .BookingApproved,
                cancellationToken);

        Assert.True(
            result.IsFailure);

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
    }

    [Theory]
    [InlineData(
        BookingNotificationType.PaymentDeadlineReminder,
        "Paid")]
    [InlineData(
        BookingNotificationType.UpcomingCheckInReminder,
        "Completed")]
    public async Task SendAsync_WhenReminderStateIsStale_ShouldSkipWithoutSendingOrRecordingDelivery(
        BookingNotificationType notificationType,
        string status)
    {
        CancellationToken cancellationToken =
            TestContext.Current.CancellationToken;

        Guid bookingId =
            Guid.NewGuid();

        BookingDetailsReadModel booking =
            CreateBooking(
                bookingId,
                status);

        var sender =
            new SpyNotificationSender();

        var deliveryStore =
            new StubNotificationDeliveryStore();

        var service =
            CreateService(
                booking,
                sender,
                deliveryStore);

        Result<BookingNotificationDispatchOutcome> result =
            await service.SendAsync(
                bookingId,
                notificationType,
                cancellationToken);

        Assert.True(
            result.IsSuccess);

        Assert.Equal(
            BookingNotificationDispatchOutcome.Skipped,
            result.Value);

        Assert.Empty(
            sender.Messages);

        Assert.False(
            deliveryStore.Contains(
                bookingId,
                notificationType));
    }

    [Fact]
    public async Task SendAsync_WhenBookingDoesNotExist_ShouldReturnNotFound()
    {
        CancellationToken cancellationToken =
            TestContext.Current.CancellationToken;

        Guid bookingId =
            Guid.NewGuid();

        var service =
            new BookingNotificationService(
                new StubBookingReadService(null),
                new SpyNotificationSender(),
                new StubNotificationDeliveryStore(),
                new FixedClock(UtcNow));

        Result<BookingNotificationDispatchOutcome> result =
            await service.SendAsync(
                bookingId,
                BookingNotificationType
                    .BookingApproved,
                cancellationToken);

        Assert.True(
            result.IsFailure);

        Assert.Equal(
            BookingNotificationErrors
                .BookingNotFound(
                    bookingId),
            result.Error);
    }

    [Fact]
    public async Task SendAsync_WhenRecipientEmailIsMissing_ShouldReturnFailureWithoutDeliveryRecord()
    {
        CancellationToken cancellationToken =
            TestContext.Current.CancellationToken;

        Guid bookingId =
            Guid.NewGuid();

        BookingDetailsReadModel booking =
            CreateBooking(
                bookingId,
                "PendingPayment");

        booking.GuestEmail =
            null;

        var deliveryStore =
            new StubNotificationDeliveryStore();

        var sender =
            new SpyNotificationSender();

        var service =
            CreateService(
                booking,
                sender,
                deliveryStore);

        Result<BookingNotificationDispatchOutcome> result =
            await service.SendAsync(
                bookingId,
                BookingNotificationType
                    .BookingApproved,
                cancellationToken);

        Assert.True(
            result.IsFailure);

        Assert.Equal(
            BookingNotificationErrors
                .RecipientUnavailable(
                    bookingId),
            result.Error);

        Assert.Empty(
            sender.Messages);

        Assert.False(
            deliveryStore.Contains(
                bookingId,
                BookingNotificationType
                    .BookingApproved));
    }

    private static BookingNotificationService CreateService(
        BookingDetailsReadModel booking,
        IBookingNotificationSender sender,
        INotificationDeliveryStore deliveryStore)
    {
        return new BookingNotificationService(
            new StubBookingReadService(
                booking),
            sender,
            deliveryStore,
            new FixedClock(UtcNow));
    }

    private static BookingDetailsReadModel CreateBooking(
        Guid bookingId,
        string status)
    {
        return new BookingDetailsReadModel
        {
            Id =
                bookingId,

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

            CancellationReason =
                nameof(
                    BookingCancellationReason
                        .PaymentExpired),

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

        public int GetByIdCallCount
        {
            get;
            private set;
        }

        public Task<BookingDetailsReadModel?>
            GetByIdAsync(
                Guid bookingId,
                CancellationToken cancellationToken = default)
        {
            cancellationToken
                .ThrowIfCancellationRequested();

            GetByIdCallCount++;

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

            if (ErrorToReturn is not null)
            {
                return Task.FromResult(
                    Result.Failure(
                        ErrorToReturn));
            }

            Messages.Add(
                message);

            return Task.FromResult(
                Result.Success());
        }
    }

    private sealed class StubNotificationDeliveryStore
        : INotificationDeliveryStore
    {
        private readonly HashSet<
            (
                Guid BookingId,
                BookingNotificationType Type
            )>
            _deliveries = [];

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

            return Task.FromResult(
                _deliveries.Add(
                    (
                        bookingId,
                        notificationType
                    )));
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

        public void Seed(
            Guid bookingId,
            BookingNotificationType notificationType)
        {
            _deliveries.Add(
                (
                    bookingId,
                    notificationType
                ));
        }
    }
}
