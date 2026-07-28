using Bookify.Services.Booking.Application.Abstractions.Persistence;
using Bookify.Services.Booking.Application.Abstractions.Persistence.Repositories;
using Bookify.Services.Booking.Application.Abstractions.Time;
using Bookify.Services.Booking.Application.Bookings;
using Bookify.Services.Booking.Application.Properties;
using Bookify.Services.Booking.Application.RentableUnits;
using Bookify.Services.Booking.Infrastructure.Persistence;
using Bookify.Services.Booking.Infrastructure.Persistence.Connections;
using Bookify.Services.Booking.Infrastructure.Persistence.Dapper;
using Bookify.Services.Booking.Infrastructure.Persistence.ReadServices;
using Bookify.Services.Booking.Infrastructure.Persistence.Repositories;
using Bookify.Services.Booking.Infrastructure.Time;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

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

        DapperTypeHandlers.Register();

        services.AddSingleton<NpgsqlDataSource>(
            _ => NpgsqlDataSource.Create(connectionString));

        services.AddDbContext<BookingDbContext>(
            (serviceProvider, options) =>
            {
                NpgsqlDataSource dataSource =
                    serviceProvider
                        .GetRequiredService<NpgsqlDataSource>();

                options.UseNpgsql(dataSource);
            });

        services.AddSingleton<
            IDbConnectionFactory,
            NpgsqlConnectionFactory>();

        AddPersistence(
            services,
            connectionString);

        return services;
    }

    private static void AddPersistence(
        IServiceCollection services,
        string connectionString)
    {
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
            DapperPropertyReadService>();

        services.AddScoped<
            IRentableUnitReadService,
            DapperRentableUnitReadService>();

        services.AddScoped<
            IBookingReadService,
            DapperBookingReadService>();
    }
}
