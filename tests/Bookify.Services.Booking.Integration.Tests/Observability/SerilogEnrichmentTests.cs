
using System.Diagnostics;
using System.Text.Json;
using Bookify.Services.Booking.Api.Observability;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Serilog.Formatting.Compact;

namespace Bookify.Services.Booking.Integration.Tests.Observability;

public sealed class SerilogEnrichmentTests
{
    [Fact]
    public void WithoutActivity_ShouldNotInventTraceIdentifiers()
    {
        using var sink = new CapturingSink();

        using var logger = new LoggerConfiguration()
            .MinimumLevel.Verbose()
            .Enrich.With<TraceContextEnricher>()
            .WriteTo.Sink(sink)
            .CreateLogger();

        logger.Information("Test log");

        LogEvent entry = Assert.Single(sink.Events);

        if (Activity.Current is null)
        {
            Assert.False(
                entry.Properties.ContainsKey("TraceId"));

            Assert.False(
                entry.Properties.ContainsKey("SpanId"));
        }
    }

    [Fact]
    public void WithActivity_ShouldIncludeTraceAndSpanIdentifiers()
    {
        using var activity = new Activity(
            "test.serilog.enrichment");

        activity.SetIdFormat(ActivityIdFormat.W3C);
        activity.Start();

        using var sink = new CapturingSink();

        using var logger = new LoggerConfiguration()
            .MinimumLevel.Verbose()
            .Enrich.With<TraceContextEnricher>()
            .WriteTo.Sink(sink)
            .CreateLogger();

        logger.Information(
            "Test event {Operation}",
            "booking");

        LogEvent entry = Assert.Single(sink.Events);

        Assert.Equal(
            activity.TraceId.ToString(),
            GetScalarValue(entry, "TraceId"));

        Assert.Equal(
            activity.SpanId.ToString(),
            GetScalarValue(entry, "SpanId"));

        Assert.Equal(
            "booking",
            GetScalarValue(entry, "Operation"));
    }

    [Fact]
    public void RenderedCompactJson_ShouldPreserveStructuredProperties()
    {
        using var sink = new CapturingSink();

        using var logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .Enrich.FromLogContext()
            .Enrich.WithProperty(
                "ServiceName",
                "Bookify.Services.Booking")
            .WriteTo.Sink(sink)
            .CreateLogger();

        logger.Information(
            "HTTP request completed {HttpMethod} {StatusCode}",
            "GET",
            200);

        LogEvent entry = Assert.Single(sink.Events);

        var formatter = new RenderedCompactJsonFormatter();

        using var writer = new StringWriter();

        formatter.Format(entry, writer);

        using JsonDocument json =
            JsonDocument.Parse(writer.ToString());

        JsonElement root = json.RootElement;

        Assert.Equal(
            "GET",
            root.GetProperty("HttpMethod").GetString());

        Assert.Equal(
            200,
            root.GetProperty("StatusCode").GetInt32());

        Assert.Equal(
            "Bookify.Services.Booking",
            root.GetProperty("ServiceName").GetString());

        Assert.True(root.TryGetProperty("@t", out _));
        Assert.True(root.TryGetProperty("@m", out _));
    }

    private static string? GetScalarValue(
        LogEvent logEvent,
        string name)
    {
        Assert.True(
            logEvent.Properties.TryGetValue(
                name,
                out LogEventPropertyValue? value));

        ScalarValue scalar = Assert.IsType<ScalarValue>(value);

        return scalar.Value?.ToString();
    }

    private sealed class CapturingSink : ILogEventSink, IDisposable
    {
        private readonly List<LogEvent> _events = [];

        public IReadOnlyList<LogEvent> Events => _events;

        public void Emit(LogEvent logEvent)
        {
            _events.Add(logEvent);
        }

        public void Dispose()
        {
            _events.Clear();
        }
    }
}
