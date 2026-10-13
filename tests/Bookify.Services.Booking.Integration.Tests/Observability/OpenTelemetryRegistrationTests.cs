
using Bookify.Services.Booking.Api.Observability;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace Bookify.Services.Booking.Integration.Tests.Observability;

public sealed class OpenTelemetryRegistrationTests
{
    [Fact]
    public void DisabledExporters_ShouldResolveProvidersWithoutCollector()
    {
        IConfiguration configuration = CreateConfiguration(
            tracingEnabled: false,
            tracingEndpoint: null,
            metricsEnabled: false,
            metricsEndpoint: null);

        var services = new ServiceCollection();

        services.AddLogging();
        services.AddBookifyOpenTelemetry(configuration);

        using ServiceProvider provider =
            services.BuildServiceProvider();

        TracerProvider tracerProvider =
            provider.GetRequiredService<TracerProvider>();

        MeterProvider meterProvider =
            provider.GetRequiredService<MeterProvider>();

        Assert.NotNull(tracerProvider);
        Assert.NotNull(meterProvider);
    }

    [Fact]
    public void EnabledTracingWithMissingEndpoint_ShouldRejectRegistration()
    {
        IConfiguration configuration = CreateConfiguration(
            tracingEnabled: true,
            tracingEndpoint: null,
            metricsEnabled: false,
            metricsEndpoint: null);

        var services = new ServiceCollection();

        Assert.Throws<InvalidOperationException>(
            () => services.AddBookifyOpenTelemetry(configuration));
    }

    [Fact]
    public void EnabledMetricsWithInvalidEndpoint_ShouldRejectRegistration()
    {
        IConfiguration configuration = CreateConfiguration(
            tracingEnabled: false,
            tracingEndpoint: null,
            metricsEnabled: true,
            metricsEndpoint: "invalid-url");

        var services = new ServiceCollection();

        Assert.Throws<InvalidOperationException>(
            () => services.AddBookifyOpenTelemetry(configuration));
    }

    [Fact]
    public void TracingEnabledAndMetricsDisabled_ShouldAcceptConfiguration()
    {
        IConfiguration configuration = CreateConfiguration(
            tracingEnabled: true,
            tracingEndpoint: "http://localhost:4318/v1/traces",
            metricsEnabled: false,
            metricsEndpoint: null);

        var services = new ServiceCollection();

        services.AddLogging();
        services.AddBookifyOpenTelemetry(configuration);

        Assert.NotEmpty(services);
    }

    [Fact]
    public void MetricsEnabledAndTracingDisabled_ShouldAcceptConfiguration()
    {
        IConfiguration configuration = CreateConfiguration(
            tracingEnabled: false,
            tracingEndpoint: null,
            metricsEnabled: true,
            metricsEndpoint:
                "http://localhost:9090/api/v1/otlp/v1/metrics");

        var services = new ServiceCollection();

        services.AddLogging();
        services.AddBookifyOpenTelemetry(configuration);

        Assert.NotEmpty(services);
    }

    private static IConfiguration CreateConfiguration(
        bool tracingEnabled,
        string? tracingEndpoint,
        bool metricsEnabled,
        string? metricsEndpoint)
    {
        var values = new Dictionary<string, string?>
        {
            ["Observability:Exporters:Tracing:Enabled"] =
                tracingEnabled.ToString(),

            ["Observability:Exporters:Tracing:Endpoint"] =
                tracingEndpoint,

            ["Observability:Exporters:Metrics:Enabled"] =
                metricsEnabled.ToString(),

            ["Observability:Exporters:Metrics:Endpoint"] =
                metricsEndpoint
        };

        return new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
    }
}
