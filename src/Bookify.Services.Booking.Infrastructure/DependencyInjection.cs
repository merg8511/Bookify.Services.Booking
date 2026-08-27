using Bookify.Services.Booking.Application.Abstractions.Idempotency;
using Bookify.Services.Booking.Application.Abstractions.Persistence;
using Bookify.Services.Booking.Application.Abstractions.Persistence.Repositories;
using Bookify.Services.Booking.Application.Abstractions.Time;
using Bookify.Services.Booking.Application.Availability;
using Bookify.Services.Booking.Application.Bookings;
using Bookify.Services.Booking.Application.Bookings.Create;
using Bookify.Services.Booking.Application.Payments.Initiate;
using Bookify.Services.Booking.Application.Properties;
using Bookify.Services.Booking.Application.RentableUnits;
using Bookify.Services.Booking.Infrastructure.Payments;
using Bookify.Services.Booking.Infrastructure.Persistence;
using Bookify.Services.Booking.Infrastructure.Persistence.Concurrency;
using Bookify.Services.Booking.Infrastructure.Persistence.Connections;
using Bookify.Services.Booking.Infrastructure.Persistence.Dapper;
using Bookify.Services.Booking.Infrastructure.Persistence.Idempotency;
using Bookify.Services.Booking.Infrastructure.Persistence.ReadServices;
using Bookify.Services.Booking.Infrastructure.Persistence.Repositories;
using Bookify.Services.Booking.Infrastructure.Persistence.Transactions;
using Bookify.Services.Booking.Infrastructure.Time;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Bookify.Services.Booking.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        string connectionString,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        ArgumentNullException.ThrowIfNull(configuration);

        // ==========================================
        //  Time & System Utilities
        // ==========================================
        services.AddSingleton<IClock, SystemClock>();
        DapperTypeHandlers.Register();


        // ==========================================
        //  Database Core Setup (EF Core & Npgsql)
        // ==========================================
        services.AddSingleton<NpgsqlDataSource>(
            _ => NpgsqlDataSource.Create(connectionString));

        services.AddDbContext<BookingDbContext>(
            (serviceProvider, options) =>
            {
                NpgsqlDataSource dataSource = serviceProvider
                    .GetRequiredService<NpgsqlDataSource>();

                options.UseNpgsql(dataSource);
            });

        services.AddSingleton<
            IDbConnectionFactory,
            NpgsqlConnectionFactory>();


        // Llamada a la persistencia (sin parámetro extra)
        AddPersistence(services, configuration);

        return services;
    }

    private static void AddPersistence(IServiceCollection services, IConfiguration configuration)
    {
        // ==========================================
        //  Transactions & Unit of Work
        // ==========================================
        services.AddScoped<IUnitOfWork>(
            serviceProvider => serviceProvider
                .GetRequiredService<BookingDbContext>());

        services.AddScoped<
            ITransactionManager,
            EfCoreTransactionManager>();


        // ==========================================
        //  Concurrency & Locks
        // ==========================================
        services.AddScoped<
            IBookingInventoryLock,
            PostgreSqlBookingInventoryLock>();

        services.AddScoped<
            IPaymentInitiationLock,
            PostgreSqlPaymentInitiationLock>();

        // ==========================================
        //  Module: Idempotency
        // ==========================================
        services.AddScoped<
            IIdempotencyStore,
            EfCoreIdempotencyStore>();

        // ==========================================
        //  Repositories (Write Side / Domain)
        // ==========================================
        services.AddScoped<
            IPropertyRepository,
            PropertyRepository>();

        services.AddScoped<
            IRentableUnitRepository,
            RentableUnitRepository>();

        services.AddScoped<
            IBookingRepository,
            BookingRepository>();


        // ==========================================
        //  Read Services (Read Side / Dapper)
        // ==========================================
        services.AddScoped<
            IPropertyReadService,
            DapperPropertyReadService>();

        services.AddScoped<
            IRentableUnitReadService,
            DapperRentableUnitReadService>();

        services.AddScoped<
            IBookingReadService,
            DapperBookingReadService>();

        services.AddScoped<
            IAvailabilityReadService,
            DapperAvailabilityReadService>();

        services.AddScoped<
            IBookingAvailabilityReader,
            DapperBookingAvailabilityReader>();

        // ==========================================
        //  Payments
        // ==========================================
        services.AddPayments(configuration);

        services.AddScoped<
            IPaymentRepository,
            PaymentRepository>();
    }
}
