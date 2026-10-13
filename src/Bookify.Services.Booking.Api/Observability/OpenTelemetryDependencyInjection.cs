using Bookify.Services.Booking.Application.Observability;
using Npgsql;
using OpenTelemetry.Exporter;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Bookify.Services.Booking.Api.Observability;

internal static class OpenTelemetryDependencyInjection
{
    private const string ServiceName = "Bookify.Services.Booking";
    private const string ApplicationActivitySource = "Bookify.Services.Booking";
    private const string BackgroundJobsActivitySource = "Bookify.Services.Booking.BackgroundJobs";

    public static IServiceCollection AddBookifyOpenTelemetry(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var exporterOptions = new ObservabilityExporterOptions();

        configuration
            .GetSection(ObservabilityExporterOptions.SectionName)
            .Bind(exporterOptions);

        Uri? tracingEndpoint = exporterOptions.Tracing.Enabled
            ? exporterOptions.Tracing.GetValidatedEndpoint("Tracing")
            : null;

        Uri? metricsEndpoint = exporterOptions.Metrics.Enabled
            ? exporterOptions.Metrics.GetValidatedEndpoint("Metrics")
            : null;

        services
            .AddOpenTelemetry()
            .ConfigureResource(resource =>
            {
                resource.AddService(serviceName: ServiceName);
            })
            .WithTracing(tracing =>
            {
                tracing
                    .AddSource(ApplicationActivitySource)
                    .AddSource(BackgroundJobsActivitySource)
                    .AddAspNetCoreInstrumentation(options => { options.RecordException = true; })
                    .AddHttpClientInstrumentation(options => { options.RecordException = true; })
                    .AddNpgsql();

                if (tracingEndpoint is not null)
                {
                    tracing
                        .AddOtlpExporter(options =>
                        {
                            options.Protocol = OtlpExportProtocol.HttpProtobuf;
                            options.Endpoint = tracingEndpoint;
                        });
                }
            })
            .WithMetrics(metrics =>
            {
                metrics
                    .AddRuntimeInstrumentation()
                    .AddAspNetCoreInstrumentation()
                    .AddMeter(BookingMetrics.MeterName);

                if (metricsEndpoint is not null)
                {
                    metrics
                        .AddOtlpExporter(options =>
                        {
                            options.Protocol = OtlpExportProtocol.HttpProtobuf;
                            options.Endpoint = metricsEndpoint;
                        });
                }
            });

        return services;
    }
}
