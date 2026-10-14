using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Net;
using Bookify.Services.Booking.Integration.Tests.Infrastructure;

namespace Bookify.Services.Booking.Integration.Tests.Observability;

[Collection(BookingApiTestFixture.Name)]
[Trait("Category", "Integration")]
public sealed class AspNetCoreTracingMetricsTests
{
    private readonly BookingApiFactory _factory;

    public AspNetCoreTracingMetricsTests(BookingApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task HealthRequest_ShouldPreserveIncomingW3CTraceAndRecordDuration()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        const string traceId = "3c941fa8ea7b4c91a719d868f89e628d";
        const string traceparent =
            "00-3c941fa8ea7b4c91a719d868f89e628d-61b79d4d289e7c30-01";

        var finished = new ConcurrentQueue<Activity>();
        var durations = new ConcurrentQueue<double>();
        var activityObserved = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var durationObserved = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        using var activityListener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == "Microsoft.AspNetCore",
            Sample = (ref ActivityCreationOptions<ActivityContext> _) =>
                ActivitySamplingResult.AllDataAndRecorded,
            SampleUsingParentId = (ref ActivityCreationOptions<string> _) =>
                ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activity =>
            {
                finished.Enqueue(activity);
                if (activity.Kind == ActivityKind.Server &&
                    activity.TraceId.ToString() == traceId)
                {
                    activityObserved.TrySetResult(true);
                }
            }
        };
        ActivitySource.AddActivityListener(activityListener);

        using var meterListener = new MeterListener
        {
            InstrumentPublished = (instrument, listener) =>
            {
                if (instrument.Meter.Name == "Microsoft.AspNetCore.Hosting" &&
                    instrument.Name == "http.server.request.duration")
                {
                    listener.EnableMeasurementEvents(instrument);
                }
            }
        };
        meterListener.SetMeasurementEventCallback<double>(
            (instrument, value, _, _) =>
            {
                durations.Enqueue(value);
                durationObserved.TrySetResult(true);
            });
        meterListener.Start();

        using var request = new HttpRequestMessage(HttpMethod.Get, "/health");
        request.Headers.TryAddWithoutValidation("traceparent", traceparent);
        request.Headers.TryAddWithoutValidation("X-Correlation-ID", "otel-api-test-001");

        using HttpResponseMessage response =
            await _factory.Client.SendAsync(request, token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("otel-api-test-001", response.Headers.GetValues("X-Correlation-ID").Single());

        await activityObserved.Task.WaitAsync(TimeSpan.FromSeconds(5), token);
        await durationObserved.Task.WaitAsync(TimeSpan.FromSeconds(5), token);

        Activity server = Assert.Single(finished, activity =>
            activity.Kind == ActivityKind.Server &&
            activity.TraceId.ToString() == traceId);

        Assert.Equal(ActivityIdFormat.W3C, server.IdFormat);
        Assert.Equal("61b79d4d289e7c30", server.ParentSpanId.ToString());
        Assert.Contains(durations, duration => duration >= 0);
    }
}
