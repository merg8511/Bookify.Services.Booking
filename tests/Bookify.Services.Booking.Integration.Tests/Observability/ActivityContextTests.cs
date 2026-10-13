
using System.Diagnostics;

namespace Bookify.Services.Booking.Integration.Tests.Observability;

public sealed class ActivityContextTests
{
    private const string BookingSource =
        "Bookify.Services.Booking";

    [Fact]
    public void ChildActivity_ShouldInheritParentTraceId()
    {
        using var listener = CreateListener();

        using var source = new ActivitySource(
            BookingSource);

        using Activity? parent = source.StartActivity(
            "test.parent",
            ActivityKind.Server);

        Assert.NotNull(parent);

        using Activity? child = source.StartActivity(
            "stripe.payment_intent.create",
            ActivityKind.Client);

        Assert.NotNull(child);

        Assert.Equal(
            ActivityIdFormat.W3C,
            child.IdFormat);

        Assert.Equal(
            parent.TraceId,
            child.TraceId);

        Assert.Equal(
            parent.SpanId,
            child.ParentSpanId);

        Assert.NotEqual(
            parent.SpanId,
            child.SpanId);
    }

    [Fact]
    public void RemoteW3CContext_ShouldPreserveTraceId()
    {
        using var listener = CreateListener();

        using var source = new ActivitySource(
            BookingSource);

        var traceId = ActivityTraceId.CreateFromString(
            "3c941fa8ea7b4c91a719d868f89e628d".AsSpan());

        var remoteSpanId = ActivitySpanId.CreateFromString(
            "61b79d4d289e7c30".AsSpan());

        var remoteContext = new ActivityContext(
            traceId,
            remoteSpanId,
            ActivityTraceFlags.Recorded,
            isRemote: true);

        using Activity? server = source.StartActivity(
            "test.incoming_request",
            ActivityKind.Server,
            remoteContext);

        Assert.NotNull(server);

        Assert.Equal(
            traceId,
            server.TraceId);

        Assert.Equal(
            remoteSpanId,
            server.ParentSpanId);

        Assert.Equal(
            ActivityIdFormat.W3C,
            server.IdFormat);
    }

    [Fact]
    public async Task ActivityContext_ShouldFlowAcrossAwait()
    {
        using var listener = CreateListener();

        using var source = new ActivitySource(
            BookingSource);

        using Activity? parent = source.StartActivity(
            "test.async_parent",
            ActivityKind.Server);

        Assert.NotNull(parent);

        await Task.Yield();

        Assert.Same(
            parent,
            Activity.Current);

        using Activity? child = source.StartActivity(
            "test.async_child",
            ActivityKind.Internal);

        Assert.NotNull(child);
        Assert.Equal(parent.TraceId, child.TraceId);
    }

    private static ActivityListener CreateListener()
    {
        var listener = new ActivityListener
        {
            ShouldListenTo = source =>
                source.Name == BookingSource,

            Sample = (ref ActivityCreationOptions<ActivityContext> _) =>
                ActivitySamplingResult.AllDataAndRecorded,

            SampleUsingParentId =
                (ref ActivityCreationOptions<string> _) =>
                    ActivitySamplingResult.AllDataAndRecorded
        };

        ActivitySource.AddActivityListener(listener);

        return listener;
    }
}
