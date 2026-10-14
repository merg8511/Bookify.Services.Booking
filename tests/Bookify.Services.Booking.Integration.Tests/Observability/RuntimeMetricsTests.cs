using System.Collections.Concurrent;
using System.Diagnostics.Metrics;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Metrics;

namespace Bookify.Services.Booking.Integration.Tests.Observability;

public sealed class RuntimeMetricsTests
{
    [Fact]
    public void RuntimeInstrumentation_ShouldObserveSystemRuntimeMeasurements()
    {
        var measurements = new ConcurrentQueue<string>();
        using var listener = new MeterListener
        {
            InstrumentPublished = (instrument, meterListener) =>
            {
                if (instrument.Meter.Name == "System.Runtime")
                {
                    meterListener.EnableMeasurementEvents(instrument);
                }
            }
        };

        listener.SetMeasurementEventCallback<long>(
            (instrument, _, _, _) => measurements.Enqueue(instrument.Name));
        listener.SetMeasurementEventCallback<int>(
            (instrument, _, _, _) => measurements.Enqueue(instrument.Name));
        listener.SetMeasurementEventCallback<double>(
            (instrument, _, _, _) => measurements.Enqueue(instrument.Name));
        listener.Start();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddOpenTelemetry().WithMetrics(builder =>
            builder.AddRuntimeInstrumentation());

        using ServiceProvider provider = services.BuildServiceProvider();
        provider.GetRequiredService<MeterProvider>();

        // Observables don't report until polled.
        listener.RecordObservableInstruments();

        Assert.NotEmpty(measurements);
        Assert.All(measurements, instrumentName =>
            Assert.False(string.IsNullOrWhiteSpace(instrumentName)));
    }
}
