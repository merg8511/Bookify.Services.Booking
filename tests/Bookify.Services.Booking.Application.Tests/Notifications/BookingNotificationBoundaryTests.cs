
using Bookify.Services.Booking.Application.Bookings;
using Bookify.Services.Booking.Application.Bookings.ReadModels;
using Bookify.Services.Booking.Application.Notifications;
using Bookify.Services.Booking.Application.Tests.Infrastructure;
using Bookify.Services.Booking.Domain.Shared;

namespace Bookify.Services.Booking.Application.Tests.Notifications;

public sealed class BookingNotificationBoundaryTests
{
    private static readonly DateTimeOffset NowUtc =
        new(2026, 10, 28, 12, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(BookingNotificationType.BookingApproved)]
    [InlineData(BookingNotificationType.PaymentDeadlineReminder)]
    public async Task SendAsync_WithoutRequiredPaymentDeadline_ShouldNotSend(
        BookingNotificationType notificationType)
    {
        // ARRANGE
        Guid bookingId = Guid.NewGuid();

        BookingDetailsReadModel booking =
            CreateBooking(bookingId);

        booking.PaymentDueAtUtc = null;

        var reader = new StubBookingReadService(booking);
        var sender = new SpyNotificationSender();
        var deliveries = new SpyDeliveryStore();

        BookingNotificationService service = CreateService(
            reader,
            sender,
            deliveries);

        // ACT
        Result<BookingNotificationDispatchOutcome> result =
            await service.SendAsync(
                bookingId,
                notificationType,
                TestContext.Current.CancellationToken);

        // ASSERT
        Assert.True(result.IsFailure);

        Assert.Equal(
            BookingNotificationErrors.RequiredDataMissing(
                bookingId,
                nameof(BookingDetailsReadModel.PaymentDueAtUtc)),
            result.Error);

        Assert.Equal(0, sender.SendCount);
        Assert.Equal(0, deliveries.RecordCount);
    }

    [Fact]
    public async Task SendAsync_WithEmptyBookingId_ShouldRejectBeforeReading()
    {
        // ARRANGE
        var reader = new StubBookingReadService(null);
        var sender = new SpyNotificationSender();
        var deliveries = new SpyDeliveryStore();

        BookingNotificationService service = CreateService(
            reader,
            sender,
            deliveries);

        // ACT
        Result<BookingNotificationDispatchOutcome> result =
            await service.SendAsync(
                Guid.Empty,
                BookingNotificationType.BookingApproved,
                TestContext.Current.CancellationToken);

        // ASSERT
        Assert.True(result.IsFailure);

        Assert.Equal(
            BookingNotificationErrors.InvalidBookingId,
            result.Error);

        Assert.Equal(0, reader.ReadCount);
        Assert.Equal(0, sender.SendCount);
        Assert.Equal(0, deliveries.CheckCount);
        Assert.Equal(0, deliveries.RecordCount);
    }

    [Fact]
    public async Task SendAsync_WhenSenderThrows_ShouldNotRecordDelivery()
    {
        // ARRANGE
        Guid bookingId = Guid.NewGuid();

        BookingDetailsReadModel booking =
            CreateBooking(bookingId);

        var reader = new StubBookingReadService(booking);

        var sender = new SpyNotificationSender
        {
            ThrowOnSend = true
        };

        var deliveries = new SpyDeliveryStore();

        BookingNotificationService service = CreateService(
            reader,
            sender,
            deliveries);

        CancellationToken cancellationToken =
            TestContext.Current.CancellationToken;

        // ACT
        Task Action() => service.SendAsync(
            bookingId,
            BookingNotificationType.BookingApproved,
            cancellationToken);

        // ASSERT
        await Assert.ThrowsAsync<InvalidOperationException>(Action);

        Assert.Equal(1, sender.SendCount);
        Assert.Equal(0, deliveries.RecordCount);
    }

    private static BookingNotificationService CreateService(
        IBookingReadService reader,
        IBookingNotificationSender sender,
        INotificationDeliveryStore deliveries)
    {
        return new BookingNotificationService(
            reader,
            sender,
            deliveries,
            new FixedClock(NowUtc));
    }

    private static BookingDetailsReadModel CreateBooking(Guid bookingId)
    {
        return new BookingDetailsReadModel
        {
            Id = bookingId,
            BookingReference = "BK-TEST-001",
            PropertyName = "Casa Bookify",
            GuestFullName = "Ada Lovelace",
            GuestEmail = "ada@example.com",
            CheckInDate = new DateOnly(2026, 11, 10),
            CheckOutDate = new DateOnly(2026, 11, 12),
            Status = "PendingPayment",
            PaymentDueAtUtc = NowUtc.AddHours(2)
        };
    }

    private sealed class StubBookingReadService : IBookingReadService
    {
        private readonly BookingDetailsReadModel? _booking;

        public StubBookingReadService(
            BookingDetailsReadModel? booking)
        {
            _booking = booking;
        }

        public int ReadCount { get; private set; }

        public Task<BookingDetailsReadModel?> GetByIdAsync(
            Guid bookingId,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            ReadCount++;

            return Task.FromResult(_booking);
        }

        public Task<BookingDetailsReadModel?> GetByReferenceAsync(
            string bookingReference,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<IReadOnlyList<BookingCalendarItemReadModel>> GetCalendarAsync(
            Guid propertyId,
            DateOnly rangeStart,
            DateOnly rangeEnd,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class SpyNotificationSender :
        IBookingNotificationSender
    {
        public int SendCount { get; private set; }

        public bool ThrowOnSend { get; set; }

        public Task<Result> SendAsync(
            BookingNotificationMessage message,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            SendCount++;

            if (ThrowOnSend)
            {
                throw new InvalidOperationException(
                    "Simulated notification provider failure.");
            }

            return Task.FromResult(Result.Success());
        }
    }

    private sealed class SpyDeliveryStore :
        INotificationDeliveryStore
    {
        public int CheckCount { get; private set; }
        public int RecordCount { get; private set; }

        public Task<bool> HasBeenDeliveredAsync(
            Guid bookingId,
            BookingNotificationType notificationType,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            CheckCount++;

            return Task.FromResult(false);
        }

        public Task<bool> TryRecordDeliveredAsync(
            Guid bookingId,
            BookingNotificationType notificationType,
            DateTimeOffset deliveredAtUtc,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            RecordCount++;

            return Task.FromResult(true);
        }
    }
}
