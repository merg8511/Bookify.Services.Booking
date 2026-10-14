using System.Collections.Concurrent;
using System.Diagnostics.Metrics;
using Bookify.Services.Booking.Application.Bookings;
using Bookify.Services.Booking.Application.Bookings.ReadModels;
using Bookify.Services.Booking.Application.Notifications;
using Bookify.Services.Booking.Application.Observability;
using Bookify.Services.Booking.Application.Tests.Infrastructure;
using Bookify.Services.Booking.Domain.Shared;

namespace Bookify.Services.Booking.Application.Tests.Observability;

[Collection("Bookify Metrics Flow")]
public sealed class NotificationDeliveryMetricsFlowTests
{
    [Theory]
    [InlineData(false, "sent")]
    [InlineData(true, "failed")]
    public async Task RealNotificationService_ShouldRecordActualSenderOutcome(
        bool shouldFail, string expectedOutcome)
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        using var capture = new DeliveryMetricCapture();
        Guid bookingId = Guid.NewGuid();

        var sender = new FakeSender(shouldFail);
        var service = CreateService(
            CreateBooking(bookingId, "PendingPayment"), sender, new FakeDeliveryStore());

        Result<BookingNotificationDispatchOutcome> result =
            await service.SendAsync(bookingId, BookingNotificationType.BookingApproved, token);

        Assert.Equal(!shouldFail, result.IsSuccess);
        Assert.Equal(1, sender.SendCount);
        DeliveryMeasurement measurement = Assert.Single(capture.Measurements);
        Assert.Equal(1L, measurement.Value);
        Assert.Equal("BookingApproved", measurement.Type);
        Assert.Equal(expectedOutcome, measurement.Outcome);
    }

    [Fact]
    public async Task AlreadyDeliveredAndSkipped_ShouldNotCountAsSendAttempts()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        using var capture = new DeliveryMetricCapture();
        Guid alreadySentId = Guid.NewGuid();
        Guid staleId = Guid.NewGuid();

        var sender = new FakeSender(shouldFail: false);
        var deliveredStore = new FakeDeliveryStore { AlreadyDelivered = true };
        var alreadyDeliveredService = CreateService(
            CreateBooking(alreadySentId, "PendingPayment"), sender, deliveredStore);

        Result<BookingNotificationDispatchOutcome> alreadyDelivered =
            await alreadyDeliveredService.SendAsync(
                alreadySentId, BookingNotificationType.BookingApproved, token);
        Assert.True(alreadyDelivered.IsSuccess);
        Assert.Equal(BookingNotificationDispatchOutcome.AlreadyDelivered, alreadyDelivered.Value);

        var staleService = CreateService(
            CreateBooking(staleId, "Paid"), sender, new FakeDeliveryStore());
        Result<BookingNotificationDispatchOutcome> skipped = await staleService.SendAsync(
            staleId, BookingNotificationType.PaymentDeadlineReminder, token);
        Assert.True(skipped.IsSuccess);
        Assert.Equal(BookingNotificationDispatchOutcome.Skipped, skipped.Value);

        Assert.Equal(0, sender.SendCount);
        Assert.Empty(capture.Measurements);
    }

    private static BookingNotificationService CreateService(
        BookingDetailsReadModel booking,
        IBookingNotificationSender sender,
        INotificationDeliveryStore store)
    {
        return new BookingNotificationService(
            new SingleBookingReader(booking), sender, store,
            new FixedClock(new DateTimeOffset(2026, 10, 13, 12, 0, 0, TimeSpan.Zero)));
    }

    private static BookingDetailsReadModel CreateBooking(Guid id, string status) => new()
    {
        Id = id,
        BookingReference = "BK-TEST-METRICS",
        PropertyName = "Test Property",
        GuestFullName = "Test Recipient",
        GuestEmail = "test-recipient@example.test",
        CheckInDate = new DateOnly(2026, 11, 10),
        CheckOutDate = new DateOnly(2026, 11, 12),
        PaymentDueAtUtc = new DateTimeOffset(2026, 10, 14, 12, 0, 0, TimeSpan.Zero),
        Status = status
    };

    private sealed class SingleBookingReader : IBookingReadService
    {
        private readonly BookingDetailsReadModel _booking;
        public SingleBookingReader(BookingDetailsReadModel booking) => _booking = booking;

        public Task<BookingDetailsReadModel?> GetByIdAsync(
            Guid bookingId, CancellationToken cancellationToken = default) =>
            Task.FromResult<BookingDetailsReadModel?>(_booking.Id == bookingId ? _booking : null);

        public Task<BookingDetailsReadModel?> GetByReferenceAsync(
            string bookingReference, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<BookingCalendarItemReadModel>> GetCalendarAsync(
            Guid propertyId, DateOnly rangeStart, DateOnly rangeEnd,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class FakeSender : IBookingNotificationSender
    {
        private readonly bool _shouldFail;
        public int SendCount { get; private set; }
        public FakeSender(bool shouldFail) => _shouldFail = shouldFail;

        public Task<Result> SendAsync(
            BookingNotificationMessage message,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            SendCount++;
            return Task.FromResult(_shouldFail
                ? Result.Failure(BookingNotificationErrors.DeliveryFailed(message.BookingId))
                : Result.Success());
        }
    }

    private sealed class FakeDeliveryStore : INotificationDeliveryStore
    {
        public bool AlreadyDelivered { get; set; }

        public Task<bool> HasBeenDeliveredAsync(
            Guid bookingId, BookingNotificationType notificationType,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(AlreadyDelivered);

        public Task<bool> TryRecordDeliveredAsync(
            Guid bookingId, BookingNotificationType notificationType,
            DateTimeOffset deliveredAtUtc,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(true);
    }

    private sealed record DeliveryMeasurement(long Value, string? Type, string? Outcome);

    private sealed class DeliveryMetricCapture : IDisposable
    {
        private readonly ConcurrentQueue<DeliveryMeasurement> _measurements = new();
        private readonly MeterListener _listener;
        public IReadOnlyList<DeliveryMeasurement> Measurements => _measurements.ToArray();

        public DeliveryMetricCapture()
        {
            _listener = new MeterListener
            {
                InstrumentPublished = (instrument, listener) =>
                {
                    if (instrument.Meter.Name == BookingMetrics.MeterName &&
                        instrument.Name == "bookify.notifications.delivery")
                    {
                        listener.EnableMeasurementEvents(instrument);
                    }
                }
            };
            _listener.SetMeasurementEventCallback<long>((instrument, value, tags, _) =>
            {
                string? type = null;
                string? outcome = null;
                foreach (KeyValuePair<string, object?> tag in tags)
                {
                    if (tag.Key == "notification.type")
                    {
                        type = tag.Value?.ToString();
                    }

                    if (tag.Key == "delivery.outcome")
                    {
                        outcome = tag.Value?.ToString();
                    }
                }
                _measurements.Enqueue(new DeliveryMeasurement(value, type, outcome));
            });
            _listener.Start();
        }

        public void Dispose() => _listener.Dispose();
    }
}
