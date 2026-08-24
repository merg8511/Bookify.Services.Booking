using Bookify.Services.Booking.Application;
using Bookify.Services.Booking.Infrastructure;
using Bookify.Services.Booking.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Bookify.Services.Booking.IntegrationTests.Infrastructure;

internal static class IntegrationTestServiceProvider
{
    public static ServiceProvider Create(string connectionString, IConfiguration configuration)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        ArgumentNullException.ThrowIfNull(configuration);

        var services = new ServiceCollection();

        services.AddLogging();

        services
            .AddApplication()
            .AddInfrastructure(
                connectionString,
                configuration);

        return services.BuildServiceProvider(
            new ServiceProviderOptions
            {
                ValidateOnBuild = true,
                ValidateScopes = true
            });
    }

    public static ServiceProvider Create(string connectionString)
    {
        var configuration = new ConfigurationManager();

        return Create(connectionString, configuration);
    }

    public static async Task ApplyMigrationsAsync(
        IServiceProvider serviceProvider,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(serviceProvider);

        await using AsyncServiceScope scope =
            serviceProvider.CreateAsyncScope();

        BookingDbContext dbContext =
            scope.ServiceProvider
                .GetRequiredService<BookingDbContext>();

        await dbContext.Database.MigrateAsync(cancellationToken);
    }
}
