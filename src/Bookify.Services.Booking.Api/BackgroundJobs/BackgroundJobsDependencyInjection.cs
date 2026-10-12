namespace Bookify.Services.Booking.Api.BackgroundJobs;

public static class BackgroundJobsDependencyInjection
{
    public static IServiceCollection AddBookifyBackgroundJobs(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services
            .AddOptions<BackgroundJobsOptions>()
            .Bind(configuration.GetSection(BackgroundJobsOptions.SectionName))
            .Validate(options => options.PollingInterval > TimeSpan.Zero,
                "BackgroundJobs:PollingInterval must be greater than zero.")
            .Validate(options => options.BatchSize > 0,
                "BackgroundJobs:BatchSize must be greater than zero.")
            .ValidateOnStart();

        services.AddHostedService<BookingBackgroundWorker>();

        return services;
    }
}
