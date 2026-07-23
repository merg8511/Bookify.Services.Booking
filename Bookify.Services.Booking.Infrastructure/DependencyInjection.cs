using Bookify.Services.Booking.Application.Abstractions.Persistence;
using Bookify.Services.Booking.Application.Abstractions.Persistence.Repositories;
using Bookify.Services.Booking.Application.Abstractions.Time;
using Bookify.Services.Booking.Application.Properties.GetById;
using Bookify.Services.Booking.Infrastructure.Persistence;
using Bookify.Services.Booking.Infrastructure.Persistence.InMemory;
using Bookify.Services.Booking.Infrastructure.Persistence.Repositories;
using Bookify.Services.Booking.Infrastructure.Time;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Bookify.Services.Booking.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        string connectionString)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        services.AddSingleton<IClock, SystemClock>();

        services.AddDbContext<BookingDbContext>(
            options =>
                options.UseNpgsql(
                    connectionString));

        AddEfCoreRepositories(services);

        AddTemporaryInMemoryPersistence(services);

        return services;
    }

    private static void AddEfCoreRepositories(
        IServiceCollection services)
    {
        services.AddScoped<PropertyRepository>();
        services.AddScoped<RentableUnitRepository>();
        services.AddScoped<BookingRepository>();
    }

    private static void AddTemporaryInMemoryPersistence(IServiceCollection services)
    {
        services.AddSingleton<InMemoryBookingStore>();

        services.AddScoped<InMemoryUnitOfWork>();

        services.AddScoped<IUnitOfWork>(
            serviceProvider =>
                serviceProvider.GetRequiredService<InMemoryUnitOfWork>());

        services.AddScoped<
            IPropertyRepository,
            InMemoryPropertyRepository>();

        services.AddScoped<
            IPropertyReadService,
            InMemoryPropertyReadService>();
    }
}
