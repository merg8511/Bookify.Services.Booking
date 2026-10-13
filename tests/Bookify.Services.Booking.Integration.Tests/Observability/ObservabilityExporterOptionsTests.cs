
using Bookify.Services.Booking.Api.Observability;

namespace Bookify.Services.Booking.Integration.Tests.Observability;

public sealed class ObservabilityExporterOptionsTests
{
    [Theory]
    [InlineData(
        "Tracing",
        "http://localhost:4318/v1/traces")]
    [InlineData(
        "Metrics",
        "http://localhost:4318/v1/metrics")]
    [InlineData(
        "Tracing",
        "https://collector.example.com/v1/traces")]
    public void EnabledDestination_WithValidEndpoint_ShouldReturnUri(
        string destination,
        string endpoint)
    {
        var options = new OtlpDestinationOptions
        {
            Enabled = true,
            Endpoint = endpoint
        };

        Uri uri = options.GetValidatedEndpoint(
            destination);

        Assert.Equal(endpoint, uri.AbsoluteUri);
    }

    [Theory]
    [InlineData("Tracing")]
    [InlineData("Metrics")]
    public void DisabledDestination_ShouldRejectEndpointResolution(
        string destination)
    {
        var options = new OtlpDestinationOptions
        {
            Enabled = false
        };

        Assert.Throws<InvalidOperationException>(
            () => options.GetValidatedEndpoint(destination));
    }

    [Theory]
    [InlineData("Tracing", null)]
    [InlineData("Tracing", "")]
    [InlineData("Tracing", "not-a-url")]
    [InlineData("Tracing", "ftp://localhost/v1/traces")]
    [InlineData("Tracing", "http://user:pass@localhost/v1/traces")]
    [InlineData("Tracing", "http://localhost/v1/traces?token=secret")]
    [InlineData("Tracing", "http://localhost/v1/traces#fragment")]
    [InlineData("Tracing", "http://localhost/v1/metrics")]
    [InlineData("Metrics", "http://localhost/v1/traces")]
    public void EnabledDestination_WithInvalidEndpoint_ShouldReject(
        string destination,
        string? endpoint)
    {
        var options = new OtlpDestinationOptions
        {
            Enabled = true,
            Endpoint = endpoint
        };

        Assert.Throws<InvalidOperationException>(
            () => options.GetValidatedEndpoint(destination));
    }

    [Fact]
    public void DefaultConfiguration_ShouldDisableBothExporters()
    {
        var options = new ObservabilityExporterOptions();

        Assert.False(options.Tracing.Enabled);
        Assert.False(options.Metrics.Enabled);
    }

    [Fact]
    public void TracingAndMetrics_ShouldHaveIndependentConfigurations()
    {
        var options = new ObservabilityExporterOptions
        {
            Tracing = new OtlpDestinationOptions
            {
                Enabled = true,
                Endpoint = "http://localhost:4318/v1/traces"
            },
            Metrics = new OtlpDestinationOptions
            {
                Enabled = false
            }
        };

        Assert.True(options.Tracing.Enabled);
        Assert.False(options.Metrics.Enabled);

        Assert.Equal(
            "/v1/traces",
            options.Tracing
                .GetValidatedEndpoint("Tracing")
                .AbsolutePath);
    }
}
