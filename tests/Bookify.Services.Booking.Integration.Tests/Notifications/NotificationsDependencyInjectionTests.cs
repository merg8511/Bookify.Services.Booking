using Bookify.Services.Booking.Application.Notifications;
using Bookify.Services.Booking.Domain.Shared;
using Bookify.Services.Booking.Infrastructure.Notifications;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Bookify.Services.Booking.Integration.Tests.Notifications;

public sealed class NotificationsDependencyInjectionTests
{
    [Fact]
    public void AddNotifications_WhenSmtpIsDisabled_ShouldResolveSenderWithoutCredentials()
    {
        // ARRANGE
        IConfiguration configuration =
            CreateConfiguration(
                new Dictionary<string, string?>
                {
                    ["Notifications:Smtp:Enabled"] =
                        "false"
                });

        var services =
            new ServiceCollection();

        services.AddLogging();

        // ACT
        services.AddNotifications(
            configuration);

        using ServiceProvider serviceProvider =
            services.BuildServiceProvider();

        IBookingNotificationSender sender =
            serviceProvider
                .GetRequiredService<
                    IBookingNotificationSender>();

        // ASSERT
        Assert.NotNull(sender);
    }

    [Fact]
    public async Task SendAsync_WhenSmtpIsDisabled_ShouldReturnDeliveryDisabled()
    {
        // ARRANGE
        IConfiguration configuration =
            CreateConfiguration(
                new Dictionary<string, string?>
                {
                    ["Notifications:Smtp:Enabled"] =
                        "false"
                });

        var services =
            new ServiceCollection();

        services.AddLogging();

        services.AddNotifications(
            configuration);

        using ServiceProvider serviceProvider =
            services.BuildServiceProvider();

        IBookingNotificationSender sender =
            serviceProvider
                .GetRequiredService<
                    IBookingNotificationSender>();

        var message =
            new BookingNotificationMessage(
                Guid.NewGuid(),
                "guest@example.com",
                "Bookify test",
                "Notification test.");

        // ACT
        Result result =
            await sender.SendAsync(
                message,
                TestContext.Current
                    .CancellationToken);

        // ASSERT
        Assert.True(result.IsFailure);

        Assert.Equal(
            BookingNotificationErrors
                .DeliveryDisabled,
            result.Error);
    }

    [Fact]
    public void ResolveSender_WhenSmtpIsEnabledWithoutHost_ShouldFailValidation()
    {
        // ARRANGE
        IConfiguration configuration =
            CreateConfiguration(
                new Dictionary<string, string?>
                {
                    ["Notifications:Smtp:Enabled"] =
                        "true",

                    ["Notifications:Smtp:Port"] =
                        "587",

                    ["Notifications:Smtp:Security"] =
                        "StartTls",

                    ["Notifications:Smtp:FromAddress"] =
                        "no-reply@bookify.test",

                    ["Notifications:Smtp:FromName"] =
                        "Bookify",

                    ["Notifications:Smtp:TimeoutMilliseconds"] =
                        "10000"
                });

        var services =
            new ServiceCollection();

        services.AddLogging();

        services.AddNotifications(
            configuration);

        using ServiceProvider serviceProvider =
            services.BuildServiceProvider();

        // ACT
        Action action =
            () => serviceProvider
                .GetRequiredService<
                    IBookingNotificationSender>();

        // ASSERT
        OptionsValidationException exception =
            Assert.Throws<
                OptionsValidationException>(
                    action);

        Assert.Contains(
            "Notifications:Smtp:Host is required",
            exception.Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public void ResolveSender_WhenSmtpConfigurationIsValid_ShouldSucceedWithoutConnecting()
    {
        // ARRANGE
        IConfiguration configuration =
            CreateConfiguration(
                new Dictionary<string, string?>
                {
                    ["Notifications:Smtp:Enabled"] =
                        "true",

                    ["Notifications:Smtp:Host"] =
                        "smtp.bookify.test",

                    ["Notifications:Smtp:Port"] =
                        "587",

                    ["Notifications:Smtp:Security"] =
                        "StartTls",

                    ["Notifications:Smtp:Username"] =
                        "bookify",

                    ["Notifications:Smtp:Password"] =
                        "test-password",

                    ["Notifications:Smtp:FromAddress"] =
                        "no-reply@bookify.test",

                    ["Notifications:Smtp:FromName"] =
                        "Bookify",

                    ["Notifications:Smtp:TimeoutMilliseconds"] =
                        "10000"
                });

        var services =
            new ServiceCollection();

        services.AddLogging();

        services.AddNotifications(
            configuration);

        using ServiceProvider serviceProvider =
            services.BuildServiceProvider();

        // ACT
        IBookingNotificationSender sender =
            serviceProvider
                .GetRequiredService<
                    IBookingNotificationSender>();

        // ASSERT
        Assert.NotNull(sender);
    }

    private static IConfiguration CreateConfiguration(
        IDictionary<string, string?> values)
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
    }
}
