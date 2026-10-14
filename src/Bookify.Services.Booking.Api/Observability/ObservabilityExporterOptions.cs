namespace Bookify.Services.Booking.Api.Observability;

using OpenTelemetry.Exporter;

internal sealed class ObservabilityExporterOptions
{
    public const string SectionName = "Observability:Exporters";
    public OtlpDestinationOptions Tracing { get; set; } = new();
    public OtlpDestinationOptions Metrics { get; set; } = new();
}

internal sealed class OtlpDestinationOptions
{
    public bool Enabled { get; set; }
    public string? Endpoint { get; set; }

    public Uri GetValidatedEndpoint(string destination)
    {
        if (!Enabled)
        {
            throw new InvalidOperationException($"The {destination} exporter is disabled.");
        }

        if (string.IsNullOrWhiteSpace(Endpoint))
        {
            throw new InvalidOperationException(
                $"Observability:Exporters:{destination}:Endpoint " +
                $"is required when the exporter is enabled.");
        }

        if (!Uri.TryCreate(Endpoint, UriKind.Absolute, out Uri? uri) ||
            (uri.Scheme != Uri.UriSchemeHttp &&
             uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new InvalidOperationException(
                $"Observability:Exporters:{destination}:Endpoint " +
                $"must be an absolute HTTP or HTTPS URL.");
        }

        if (!string.IsNullOrEmpty(uri.UserInfo) ||
            !string.IsNullOrEmpty(uri.Query) ||
            !string.IsNullOrEmpty(uri.Fragment))
        {
            throw new InvalidOperationException(
                $"Observality:Exporters:{destination}:Endpoint " +
                $"must not contain credentials, query strings or fragments.");
        }

        string expectedSuffix = destination switch
        {
            "Tracing" => "/v1/traces",
            "Metrics" => "/v1/metrics",
            _ => throw new ArgumentOutOfRangeException(nameof(destination))
        };

        if (!uri.AbsolutePath.EndsWith(expectedSuffix, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Observability:Exporters:{destination}:Endpoint " +
                $"must end with '{expectedSuffix}'");
        }

        return uri;
    }
}
