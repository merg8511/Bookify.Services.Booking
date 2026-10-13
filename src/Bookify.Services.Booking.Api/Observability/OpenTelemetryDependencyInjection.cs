using Bookify.Services.Booking.Application.Observability;
using Npgsql;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Bookify.Services.Booking.Api.Observability;

internal static class OpenTelemetryDependencyInjection
{
    private const string ServiceName = "Bookify.Services.Booking";
    private const string ApplicationActivitySource = "Bookify.Services.Booking";
    private const string BackgroundJobsActivitySource = "Bookify.Services.Booking.BackgroundJobs";

    public static IServiceCollection AddBookifyOpenTelemetry(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

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
            })
            .WithMetrics(metrics =>
            {
                metrics
                    .AddRuntimeInstrumentation()
                    .AddAspNetCoreInstrumentation()
                    .AddMeter(BookingMetrics.MeterName);
            });

        return services;
    }
}
