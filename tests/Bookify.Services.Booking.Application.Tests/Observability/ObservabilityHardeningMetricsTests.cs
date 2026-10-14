
using System.Collections.Concurrent;
using System.Diagnostics.Metrics;
using Bookify.Services.Booking.Application.Abstractions.BackgroundJobs;
using Bookify.Services.Booking.Application.Observability;
using Bookify.Services.Booking.Domain.Payments;

namespace Bookify.Services.Booking.Application.Tests.Observability;

[Collection("Bookify Metrics Flow")]
public sealed class ObservabilityHardeningMetricsTests
{
    [Theory]
    [InlineData(
        PaymentStatus.Pending,
        PaymentStatus.Succeeded,
        "succeeded")]
    [InlineData(
        PaymentStatus.Pending,
        PaymentStatus.Failed,
        "failed")]
    [InlineData(
        PaymentStatus.Failed,
        PaymentStatus.Succeeded,
        "succeeded")]
    public void PaymentTransition_WhenStatusChangesToTerminal_ShouldCount(
        PaymentStatus before,
        PaymentStatus after,
        string expectedStatus)
    {
        using var capture = new MetricCapture(
            "bookify.payments.transitions");

        BookingMetrics.RecordPaymentTransition(before, after);

        Measurement measurement =
            Assert.Single(capture.Measurements);

        Assert.Equal(1L, measurement.Value);

        Assert.Equal(
            expectedStatus,
            measurement.Tags["payment.status"]);
    }

    [Theory]
    [InlineData(PaymentStatus.Pending, PaymentStatus.Pending)]
    [InlineData(PaymentStatus.Succeeded, PaymentStatus.Succeeded)]
    [InlineData(PaymentStatus.Failed, PaymentStatus.Failed)]
    [InlineData(PaymentStatus.Failed, PaymentStatus.Pending)]
    public void PaymentTransition_WithoutRelevantChange_ShouldNotCount(
        PaymentStatus before,
        PaymentStatus after)
    {
        using var capture = new MetricCapture(
            "bookify.payments.transitions");

        BookingMetrics.RecordPaymentTransition(before, after);

        Assert.Empty(capture.Measurements);
    }

    [Fact]
    public void JobItemFailed_ShouldCountKnownJob()
    {
        using var capture = new MetricCapture(
            "bookify.jobs.items.failed");

        BookingMetrics.RecordJobItemFailed(
            BackgroundJobClaimKeys.ApprovalExpiration);

        Measurement measurement =
            Assert.Single(capture.Measurements);

        Assert.Equal(1L, measurement.Value);

        Assert.Equal(
            BackgroundJobClaimKeys.ApprovalExpiration,
            measurement.Tags["job.key"]);
    }

    [Fact]
    public void JobItemFailed_WhenJobKeyIsUnknown_ShouldReject()
    {
        using var capture = new MetricCapture(
            "bookify.jobs.items.failed");

        Assert.Throws<ArgumentOutOfRangeException>(
            () => BookingMetrics.RecordJobItemFailed(
                "untrusted-customer-value"));

        Assert.Empty(capture.Measurements);
    }

    [Fact]
    public void JobItemFailed_ShouldCountAttemptsNotUniqueItems()
    {
        using var capture = new MetricCapture(
            "bookify.jobs.items.failed");

        BookingMetrics.RecordJobItemFailed(
            BackgroundJobClaimKeys.OutboxNotification);

        BookingMetrics.RecordJobItemFailed(
            BackgroundJobClaimKeys.OutboxNotification);

        Assert.Equal(
            2L,
            capture.Measurements.Sum(x => x.Value));
    }

    private sealed record Measurement(
        long Value,
        IReadOnlyDictionary<string, object?> Tags);

    private sealed class MetricCapture : IDisposable
    {
        private readonly ConcurrentQueue<Measurement> _measurements = new();
        private readonly MeterListener _listener;
        private readonly string _instrumentName;

        public MetricCapture(string instrumentName)
        {
            _instrumentName = instrumentName;

            _listener = new MeterListener
            {
                InstrumentPublished = (instrument, listener) =>
                {
                    if (instrument.Meter.Name ==
                            BookingMetrics.MeterName &&
                        instrument.Name == _instrumentName)
                    {
                        listener.EnableMeasurementEvents(instrument);
                    }
                }
            };

            _listener.SetMeasurementEventCallback<long>(
                (_, value, tags, _) =>
                {
                    var properties =
                        new Dictionary<string, object?>(
                            StringComparer.Ordinal);

                    foreach (KeyValuePair<string, object?> tag in tags)
                    {
                        properties[tag.Key] = tag.Value;
                    }

                    _measurements.Enqueue(
                        new Measurement(value, properties));
                });

            _listener.Start();
        }

        public IReadOnlyList<Measurement> Measurements =>
            _measurements.ToArray();

        public void Dispose()
        {
            _listener.Dispose();
        }
    }
}
