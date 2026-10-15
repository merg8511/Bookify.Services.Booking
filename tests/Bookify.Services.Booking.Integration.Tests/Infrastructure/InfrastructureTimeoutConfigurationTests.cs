using Bookify.Services.Booking.Application.Abstractions.Payments;
using Bookify.Services.Booking.Application.Notifications;
using Bookify.Services.Booking.Infrastructure.Notifications;
using Bookify.Services.Booking.Infrastructure.Payments;
using Bookify.Services.Booking.Infrastructure.Payments.Stripe;
using Bookify.Services.Booking.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Npgsql;
using Stripe;

namespace Bookify.Services.Booking.Integration.Tests.Infrastructure;

[Trait("Category", "Integration")]
public sealed class InfrastructureTimeoutConfigurationTests
{
    [Fact]
    public void PostgreSql_ShouldApplyTimeoutsToDataSourceAndEfCore()
    {
        IConfiguration configuration = CreateConfiguration(
            new Dictionary<string, string?>
            {
                ["Payments:Provider"] = "Fake",
                ["PostgreSql:ConnectionTimeoutSeconds"] = "7",
                ["PostgreSql:CommandTimeoutSeconds"] = "19"
            });

        const string connectionString =
            "Host=localhost;" +
            "Port=5432;" +
            "Database=bookify_test;" +
            "Username=bookify;" +
            "Password=test_password";

        using ServiceProvider serviceProvider =
            IntegrationTestServiceProvider.Create(
                connectionString,
                configuration);

        NpgsqlDataSource dataSource =
            serviceProvider.GetRequiredService<NpgsqlDataSource>();

        var settings =
            new NpgsqlConnectionStringBuilder(
                dataSource.ConnectionString);

        Assert.Equal(7, settings.Timeout);

        Assert.Equal(19, settings.CommandTimeout);

        using IServiceScope scope =
            serviceProvider.CreateScope();

        BookingDbContext dbContext =
            scope.ServiceProvider
                .GetRequiredService<BookingDbContext>();

        Assert.Equal(
            19,
            dbContext.Database.GetCommandTimeout());
    }

    [Fact]
    public void Stripe_ShouldResolveGatewayWithoutAutomaticRetries()
    {
        IConfiguration configuration = CreateConfiguration(
            new Dictionary<string, string?>
            {
                ["Payments:Provider"] = "Stripe",
                ["Payments:Stripe:SecretKey"] =
                    "sk_test_bookify",
                ["Payments:Stripe:WebhookSecret"] =
                    "whsec_bookify_tests",
                ["Payments:Stripe:WebhookToleranceSeconds"] =
                    "300",
                ["Payments:Stripe:HttpTimeoutSeconds"] =
                    "20"
            });

        var services = new ServiceCollection();

        services.AddPayments(configuration);

        using ServiceProvider serviceProvider =
            services.BuildServiceProvider();

        IStripeClient registeredStripeClient =
    serviceProvider.GetRequiredService<IStripeClient>();

        StripeClient stripeClient =
            Assert.IsType<StripeClient>(
                registeredStripeClient);

        SystemNetHttpClient httpClient =
            Assert.IsType<SystemNetHttpClient>(
                stripeClient.HttpClient);

        Assert.Equal(
            0,
            httpClient.MaxNetworkRetries);

        IPaymentGateway gateway =
            serviceProvider
                .GetRequiredService<IPaymentGateway>();

        Assert.IsType<StripePaymentGateway>(gateway);
    }

    [Fact]
    public void Smtp_WhenTimeoutExceedsLimit_ShouldFailValidation()
    {
        IConfiguration configuration = CreateConfiguration(
            new Dictionary<string, string?>
            {
                ["Notifications:Smtp:Enabled"] = "true",
                ["Notifications:Smtp:Host"] =
                    "smtp.bookify.test",
                ["Notifications:Smtp:Port"] = "587",
                ["Notifications:Smtp:Security"] = "StartTls",
                ["Notifications:Smtp:FromName"] = "Bookify",
                ["Notifications:Smtp:FromAddress"] =
                    "no-reply@bookify.test",
                ["Notifications:Smtp:TimeoutMilliseconds"] =
                    "300001"
            });

        var services = new ServiceCollection();

        services.AddLogging();

        services.AddNotifications(configuration);

        using ServiceProvider serviceProvider =
            services.BuildServiceProvider();

        OptionsValidationException exception =
            Assert.Throws<OptionsValidationException>(
                () => serviceProvider
                    .GetRequiredService<
                        IBookingNotificationSender>());

        Assert.Contains(
            "Notifications:Smtp:TimeoutMilliseconds",
            exception.Message,
            StringComparison.Ordinal);
    }

    private static IConfiguration CreateConfiguration(
        IDictionary<string, string?> values)
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
    }
}
