
using System.Diagnostics.Metrics;
using Bookify.Services.Booking.Application.Observability;

namespace Bookify.Services.Booking.Application.Tests.Observability;

public sealed class BookingMetricsTests
{
    [Fact]
    public void RecordBookingCreated_ShouldIncrementCounter()
    {
        using var capture = new MetricCapture(
            "bookify.bookings.created");

        BookingMetrics.RecordBookingCreated();

        MetricMeasurement measurement =
            Assert.Single(capture.Measurements);

        Assert.Equal(1L, measurement.Value);
        Assert.Empty(measurement.Tags);
    }

    [Fact]
    public void RecordPaymentOutcome_ShouldRecordSucceeded()
    {
        using var capture = new MetricCapture(
            "bookify.payments.outcomes");

        BookingMetrics.RecordPaymentOutcome("succeeded");

        MetricMeasurement measurement =
            Assert.Single(capture.Measurements);

        Assert.Equal(1L, measurement.Value);
        Assert.Equal(
            "succeeded",
            measurement.Tags["payment.status"]);
    }

    [Fact]
    public void RecordPaymentOutcome_ShouldRecordFailed()
    {
        using var capture = new MetricCapture(
            "bookify.payments.outcomes");

        BookingMetrics.RecordPaymentOutcome("failed");

        MetricMeasurement measurement =
            Assert.Single(capture.Measurements);

        Assert.Equal(1L, measurement.Value);
        Assert.Equal(
            "failed",
            measurement.Tags["payment.status"]);
    }

    [Theory]
    [InlineData("")]
    [InlineData("pending")]
    [InlineData("succeeded_customer_123")]
    public void RecordPaymentOutcome_WhenUnsupported_ShouldReject(
        string status)
    {
        using var capture = new MetricCapture(
            "bookify.payments.outcomes");

        Assert.ThrowsAny<ArgumentException>(
            () => BookingMetrics.RecordPaymentOutcome(status));

        Assert.Empty(capture.Measurements);
    }

    [Fact]
    public void RecordDuplicateWebhook_ShouldIncrementCounter()
    {
        using var capture = new MetricCapture(
            "bookify.webhooks.duplicates");

        BookingMetrics.RecordDuplicateWebhook();

        MetricMeasurement measurement =
            Assert.Single(capture.Measurements);

        Assert.Equal(1L, measurement.Value);
        Assert.Equal(
            "stripe",
            measurement.Tags["webhook.provider"]);
    }

    [Fact]
    public void RecordJobProcessed_ShouldRecordProcessedCount()
    {
        using var capture = new MetricCapture(
            "bookify.jobs.processed");

        BookingMetrics.RecordJobProcessed(
            "ApprovalExpirationJob",
            5);

        MetricMeasurement measurement =
            Assert.Single(capture.Measurements);

        Assert.Equal(5L, measurement.Value);
        Assert.Equal(
            "ApprovalExpirationJob",
            measurement.Tags["job.name"]);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void RecordJobProcessed_WhenCountIsNotPositive_ShouldIgnore(
        int processedCount)
    {
        using var capture = new MetricCapture(
            "bookify.jobs.processed");

        BookingMetrics.RecordJobProcessed(
            "ApprovalExpirationJob",
            processedCount);

        Assert.Empty(capture.Measurements);
    }

    [Fact]
    public void RecordJobFailed_ShouldIncrementCounter()
    {
        using var capture = new MetricCapture(
            "bookify.jobs.failed");

        BookingMetrics.RecordJobFailed(
            "OutboxNotificationJob");

        MetricMeasurement measurement =
            Assert.Single(capture.Measurements);

        Assert.Equal(1L, measurement.Value);
        Assert.Equal(
            "OutboxNotificationJob",
            measurement.Tags["job.name"]);
    }

    [Theory]
    [InlineData("sent")]
    [InlineData("failed")]
    public void RecordNotificationDelivery_ShouldRecordOutcome(
        string outcome)
    {
        using var capture = new MetricCapture(
            "bookify.notifications.delivery");

        BookingMetrics.RecordNotificationDelivery(
            "BookingApproved",
            outcome);

        MetricMeasurement measurement =
            Assert.Single(capture.Measurements);

        Assert.Equal(1L, measurement.Value);
        Assert.Equal(
            "BookingApproved",
            measurement.Tags["notification.type"]);

        Assert.Equal(
            outcome,
            measurement.Tags["delivery.outcome"]);
    }

    [Fact]
    public void RecordNotificationDelivery_ShouldRejectUnexpectedOutcome()
    {
        using var capture = new MetricCapture(
            "bookify.notifications.delivery");

        Assert.Throws<ArgumentOutOfRangeException>(
            () => BookingMetrics.RecordNotificationDelivery(
                "BookingApproved",
                "customer-123"));

        Assert.Empty(capture.Measurements);
    }

    [Fact]
    public void Measurements_ShouldNotContainHighCardinalityIdentifiers()
    {
        using var capture = new MetricCapture(
            "bookify.payments.outcomes",
            "bookify.webhooks.duplicates",
            "bookify.jobs.failed",
            "bookify.notifications.delivery");

        BookingMetrics.RecordPaymentOutcome("succeeded");
        BookingMetrics.RecordDuplicateWebhook();
        BookingMetrics.RecordJobFailed("BookingCompletionJob");
        BookingMetrics.RecordNotificationDelivery(
            "PaymentConfirmed",
            "sent");

        Assert.Equal(4, capture.Measurements.Length);

        string[] prohibitedTagNames =
        [
            "booking.id",
            "payment.id",
            "customer.id",
            "correlation.id",
            "trace.id",
            "email",
            "token",
            "client_secret",
            "idempotency_key"
        ];

        foreach (MetricMeasurement measurement
            in capture.Measurements)
        {
            foreach (string prohibitedTagName
                in prohibitedTagNames)
            {
                Assert.False(
                    measurement.Tags.ContainsKey(
                        prohibitedTagName));
            }
        }
    }

    private sealed record MetricMeasurement(
        string InstrumentName,
        long Value,
        IReadOnlyDictionary<string, object?> Tags);

    private sealed class MetricCapture : IDisposable
    {
        private readonly MeterListener _listener;
        private readonly HashSet<string> _instrumentNames;
        private readonly List<MetricMeasurement> _measurements = [];
        private readonly object _sync = new();

        public MetricCapture(params string[] instrumentNames)
        {
            _instrumentNames = new HashSet<string>(
                instrumentNames,
                StringComparer.Ordinal);

            _listener = new MeterListener
            {
                InstrumentPublished = (instrument, listener) =>
                {
                    if (instrument.Meter.Name ==
                            BookingMetrics.MeterName &&
                        _instrumentNames.Contains(instrument.Name))
                    {
                        listener.EnableMeasurementEvents(instrument);
                    }
                }
            };

            _listener.SetMeasurementEventCallback<long>(
                (instrument, measurement, tags, _) =>
                {
                    var properties =
                        new Dictionary<string, object?>(
                            StringComparer.Ordinal);

                    foreach (KeyValuePair<string, object?> tag
                        in tags)
                    {
                        properties[tag.Key] = tag.Value;
                    }

                    lock (_sync)
                    {
                        _measurements.Add(
                            new MetricMeasurement(
                                instrument.Name,
                                measurement,
                                properties));
                    }
                });

            _listener.Start();
        }

        public MetricMeasurement[] Measurements
        {
            get
            {
                lock (_sync)
                {
                    return _measurements.ToArray();
                }
            }
        }

        public void Dispose()
        {
            _listener.Dispose();
        }
    }
}
