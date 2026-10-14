using Bookify.Services.Booking.Api.Observability;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace Bookify.Services.Booking.Integration.Tests.Observability;

public sealed class OtlpExportersStartupTests
{
    [Fact]
    public void BothExportersEnabled_ShouldRegisterBothSignalProviders()
    {
        IConfiguration config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Observability:Exporters:Tracing:Enabled"] = "true",
                ["Observability:Exporters:Tracing:Endpoint"] =
                    "http://127.0.0.1:4318/v1/traces",
                ["Observability:Exporters:Metrics:Enabled"] = "true",
                ["Observability:Exporters:Metrics:Endpoint"] =
                    "http://127.0.0.1:9090/api/v1/otlp/v1/metrics"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddBookifyOpenTelemetry(config);

        // Resolve the real providers, not just their service descriptors.
        // No Jaeger or Prometheus process is started in this test.
        using ServiceProvider provider = services.BuildServiceProvider();
        Assert.NotNull(provider.GetRequiredService<TracerProvider>());
        Assert.NotNull(provider.GetRequiredService<MeterProvider>());
    }
}
