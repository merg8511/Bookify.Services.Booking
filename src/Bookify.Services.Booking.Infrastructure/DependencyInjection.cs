using Bookify.Services.Booking.Application.Abstractions.BackgroundJobs;
using Bookify.Services.Booking.Application.Abstractions.Idempotency;
using Bookify.Services.Booking.Application.Abstractions.Payments.Webhooks;
using Bookify.Services.Booking.Application.Abstractions.Persistence;
using Bookify.Services.Booking.Application.Abstractions.Persistence.Repositories;
using Bookify.Services.Booking.Application.Abstractions.Time;
using Bookify.Services.Booking.Application.Availability;
using Bookify.Services.Booking.Application.BackgroundJobs;
using Bookify.Services.Booking.Application.Bookings;
using Bookify.Services.Booking.Application.Bookings.Create;
using Bookify.Services.Booking.Application.Notifications;
using Bookify.Services.Booking.Application.Outbox;
using Bookify.Services.Booking.Application.Payments;
using Bookify.Services.Booking.Application.Payments.Initiate;
using Bookify.Services.Booking.Application.Properties;
using Bookify.Services.Booking.Application.RentableUnits;
using Bookify.Services.Booking.Infrastructure.Bookings;
using Bookify.Services.Booking.Infrastructure.Notifications;
using Bookify.Services.Booking.Infrastructure.Payments;
using Bookify.Services.Booking.Infrastructure.Persistence;
using Bookify.Services.Booking.Infrastructure.Persistence.Concurrency;
using Bookify.Services.Booking.Infrastructure.Persistence.Connections;
using Bookify.Services.Booking.Infrastructure.Persistence.Dapper;
using Bookify.Services.Booking.Infrastructure.Persistence.Idempotency;
using Bookify.Services.Booking.Infrastructure.Persistence.Notifications;
using Bookify.Services.Booking.Infrastructure.Persistence.Outbox;
using Bookify.Services.Booking.Infrastructure.Persistence.Payments.Webhooks;
using Bookify.Services.Booking.Infrastructure.Persistence.ReadServices;
using Bookify.Services.Booking.Infrastructure.Persistence.Repositories;
using Bookify.Services.Booking.Infrastructure.Persistence.Transactions;
using Bookify.Services.Booking.Infrastructure.Time;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
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
        // Time & System Utilities
        // ==========================================
        services.AddSingleton<IClock, SystemClock>();

        DapperTypeHandlers.Register();

        services
            .AddOptions<OutboxRetryOptions>()
            .Bind(configuration.GetSection(OutboxRetryOptions.SectionName))
            .Validate(options =>
                    options.MaxAttempts is >= 1 and <= 20 &&
                    options.BaseDelay > TimeSpan.Zero &&
                    options.MaxDelay >= options.BaseDelay &&
                    options.MaxDelay <= TimeSpan.FromDays(1) &&
                    options.DisabledDelay > TimeSpan.Zero &&
                    options.DisabledDelay <= TimeSpan.FromDays(1),
                "OutboxRetry configuration contains invalid values.")
            .ValidateOnStart();

        // ==========================================
        // PostgreSQL Timeouts 
        // ==========================================
        services
            .AddOptions<PostgreSqlTimeoutOptions>()
            .Bind(configuration.GetSection(PostgreSqlTimeoutOptions.SectionName))
            .Validate(options =>
                        options.ConnectionTimeoutSeconds is >= 1 and <= 60 &&
                        options.CommandTimeoutSeconds is >= 1 and <= 300,
                        "PostgreSql timeout values are invalid. " +
                        "ConnectionTimeoutSeconds must be between 1 and 60. " +
                        "CommandTimeoutSeconds must be between 1 and 300.")
            .ValidateOnStart();

        // ==========================================
        // Database Core Setup
        // ==========================================
        services.AddSingleton<NpgsqlDataSource>(
            serviceProvider =>
            {
                PostgreSqlTimeoutOptions timeoutOptions = serviceProvider
                    .GetRequiredService<IOptions<PostgreSqlTimeoutOptions>>().Value;

                var connectionStringBuilder = new NpgsqlConnectionStringBuilder(connectionString)
                {
                    Timeout = timeoutOptions.ConnectionTimeoutSeconds,
                    CommandTimeout = timeoutOptions.CommandTimeoutSeconds
                };

                return NpgsqlDataSource.Create(connectionStringBuilder.ConnectionString);
            });

        services.AddDbContext<BookingDbContext>((serviceProvider, options) =>
            {
                NpgsqlDataSource dataSource = serviceProvider.GetRequiredService<NpgsqlDataSource>();

                PostgreSqlTimeoutOptions timeoutOptions = serviceProvider
                    .GetRequiredService<IOptions<PostgreSqlTimeoutOptions>>().Value;

                options.UseNpgsql(
                    dataSource,
                    npgsqlOptions => npgsqlOptions.CommandTimeout(timeoutOptions.CommandTimeoutSeconds));

                options.EnableSensitiveDataLogging(false);
            });

        services.AddSingleton<
            IDbConnectionFactory,
            NpgsqlConnectionFactory>();

        // ==========================================
        // Persistence
        // ==========================================
        AddPersistence(services);

        // ==========================================
        // Module Configuration
        // ==========================================
        services.AddBookings(configuration);
        services.AddPayments(configuration);

        NotificationsDependencyInjection.AddNotifications(services, configuration);

        return services;
    }

    private static void AddPersistence(IServiceCollection services)
    {
        // ==========================================
        // Transactions & Unit of Work
        // ==========================================
        services.AddScoped<IUnitOfWork>(serviceProvider =>
                serviceProvider.GetRequiredService<BookingDbContext>());

        services.AddScoped<
            ITransactionManager,
            EfCoreTransactionManager>();

        // ==========================================
        // Concurrency & Locks
        // ==========================================
        services.AddScoped<
            IBookingInventoryLock,
            PostgreSqlBookingInventoryLock>();

        services.AddScoped<
            IPaymentInitiationLock,
            PostgreSqlPaymentInitiationLock>();

        services.AddScoped<
            IBackgroundJobClaimManager,
            PostgreSqlBackgroundJobClaimManager>();

        // ==========================================
        // Idempotency & Webhook Persistence
        // ==========================================
        services.AddScoped<
            IIdempotencyStore,
            EfCoreIdempotencyStore>();

        services.AddScoped<
            IPaymentWebhookEventStore,
            EfCorePaymentWebhookEventStore>();

        services.AddScoped<
            IOutboxMessageStore,
            EfCoreOutboxMessageStore>();

        // ==========================================
        // Repositories (Write Side)
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

        services.AddScoped<
            IPaymentRepository,
            PaymentRepository>();

        // ==========================================
        // Read Services (Dapper)
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

        services.AddScoped<
            IPaymentReadService,
            DapperPaymentReadService>();

        services.AddScoped<
            IPendingBookingWorkSelector,
            DapperPendingBookingWorkSelector>();

        services.AddScoped<
            IOutboxMessageReader,
            DapperOutboxMessageReader>();

        services.AddScoped<
            INotificationDeliveryStore, EfCoreNotificationDeliveryStore>();
    }
}
