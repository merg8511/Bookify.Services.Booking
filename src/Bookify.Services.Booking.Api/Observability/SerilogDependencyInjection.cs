using Serilog;
using Serilog.Formatting.Compact;

namespace Bookify.Services.Booking.Api.Observability;

internal static class SerilogDependencyInjection
{
    public static IServiceCollection AddBookifySerilog(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddSerilog((serviceProvider, loggerConfiguration) =>
        {
            loggerConfiguration
                .ReadFrom.Configuration(configuration)
                .Enrich.FromLogContext()
                .Enrich.With<TraceContextEnricher>()
                .Enrich.WithProperty(
                    "ServiceName",
                    "Bookify.Services.Booking")
                .WriteTo.Console(new RenderedCompactJsonFormatter());
        });

        return services;
    }
}
