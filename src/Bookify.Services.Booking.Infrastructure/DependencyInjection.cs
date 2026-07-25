using Bookify.Services.Booking.Application.Abstractions.Persistence;
using Bookify.Services.Booking.Application.Abstractions.Persistence.Repositories;
using Bookify.Services.Booking.Application.Abstractions.Time;
using Bookify.Services.Booking.Application.Properties.GetById;
using Bookify.Services.Booking.Infrastructure.Persistence;
using Bookify.Services.Booking.Infrastructure.Persistence.ReadServices;
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

        AddPersistence(
            services,
            connectionString);

        return services;
    }

    private static void AddPersistence(
        IServiceCollection services,
        string connectionString)
    {
        services.AddDbContext<BookingDbContext>(
            options =>
                options.UseNpgsql(connectionString));

        services.AddScoped<IUnitOfWork>(
            serviceProvider =>
                serviceProvider.GetRequiredService<BookingDbContext>());

        services.AddScoped<
            IPropertyRepository,
            PropertyRepository>();

        services.AddScoped<
            IRentableUnitRepository,
            RentableUnitRepository>();

        services.AddScoped<
            IBookingRepository,
            BookingRepository>();

        services.AddScoped<
            IPropertyReadService,
            EfCorePropertyReadService>();
    }
}
