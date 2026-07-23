using Bookify.Services.Booking.Application;
using Bookify.Services.Booking.Infrastructure;
using Bookify.Services.Booking.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Bookify.Services.Booking.IntegrationTests.Infrastructure;

internal static class IntegrationTestServiceProvider
{
    public static ServiceProvider Create(string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        var services = new ServiceCollection();

        services.AddLogging();

        services
            .AddApplication()
            .AddInfrastructure(connectionString);

        return services.BuildServiceProvider(
            new ServiceProviderOptions
            {
                ValidateOnBuild = true,
                ValidateScopes = true
            });
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
