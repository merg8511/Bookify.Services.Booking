using Bookify.Services.Booking.Application.Abstractions.Payments;
using Bookify.Services.Booking.Infrastructure.Payments;
using Bookify.Services.Booking.Infrastructure.Payments.Fake;
using Bookify.Services.Booking.Infrastructure.Payments.Stripe;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Bookify.Services.Booking.Integration.Tests.Payments;

public sealed class PaymentsDependencyInjectionTests
{
    [Fact]
    public void AddPayments_WithFakeProvider_ShouldResolveFakeGateway()
    {
        // Arrange
        IConfiguration configuration =
            CreateConfiguration(
                new Dictionary<string, string?>
                {
                    ["Payments:Provider"] = "Fake"
                });

        var services =
            new ServiceCollection();

        // Act
        services.AddPayments(configuration);

        using ServiceProvider serviceProvider =
            services.BuildServiceProvider();

        IPaymentGateway gateway =
            serviceProvider.GetRequiredService<IPaymentGateway>();

        // Assert
        Assert.IsType<FakePaymentGateway>(gateway);
    }

    [Fact]
    public void AddPayments_WithStripeProvider_ShouldResolveStripeGateway()
    {
        // Arrange
        IConfiguration configuration =
            CreateConfiguration(
                new Dictionary<string, string?>
                {
                    ["Payments:Provider"] = "Stripe",
                    ["Payments:Stripe:SecretKey"] = "sk_test_bookify",
                    ["Payments:Stripe:WebhookSecret"] = "whsec_bookify_tests",
                    ["Payments:Stripe:WebhookToleranceSeconds"] = "300"
                });

        var services =
            new ServiceCollection();

        // Act
        services.AddPayments(configuration);

        using ServiceProvider serviceProvider =
            services.BuildServiceProvider();

        IPaymentGateway gateway =
            serviceProvider.GetRequiredService<IPaymentGateway>();

        // Assert
        Assert.IsType<StripePaymentGateway>(gateway);
    }

    [Theory]
    [InlineData("")]
    [InlineData("Unknown")]
    [InlineData("PayPal")]
    public void AddPayments_WithInvalidProvider_ShouldThrow(
        string provider)
    {
        // Arrange
        IConfiguration configuration =
            CreateConfiguration(
                new Dictionary<string, string?>
                {
                    ["Payments:Provider"] = provider
                });

        var services =
            new ServiceCollection();

        // Act
        Action action =
            () => services.AddPayments(configuration);

        // Assert
        InvalidOperationException exception =
            Assert.Throws<InvalidOperationException>(action);

        Assert.Contains(
            "Payment provider",
            exception.Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public void ResolveStripeGateway_WithMissingSecretKey_ShouldThrowOptionsValidationException()
    {
        // Arrange
        IConfiguration configuration =
            CreateConfiguration(
                new Dictionary<string, string?>
                {
                    ["Payments:Provider"] = "Stripe",
                    ["Payments:Stripe:WebhookSecret"] = "whsec_bookify_tests",
                    ["Payments:Stripe:WebhookToleranceSeconds"] = "300"
                });

        var services =
            new ServiceCollection();

        services.AddPayments(configuration);

        using ServiceProvider serviceProvider =
            services.BuildServiceProvider();

        // Act
        Action action =
            () => serviceProvider
                .GetRequiredService<IPaymentGateway>();

        // Assert
        OptionsValidationException exception =
            Assert.Throws<OptionsValidationException>(action);

        Assert.Contains(
            "Payments:Stripe:SecretKey is required",
            exception.Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public void ResolveStripeGateway_WithMissingWebhookSecret_ShouldThrowOptionsValidationException()
    {
        // Arrange
        IConfiguration configuration =
            CreateConfiguration(
                new Dictionary<string, string?>
                {
                    ["Payments:Provider"] = "Stripe",
                    ["Payments:Stripe:SecretKey"] = "sk_test_bookify",
                    ["Payments:Stripe:WebhookToleranceSeconds"] = "300"
                });

        var services =
            new ServiceCollection();

        services.AddPayments(configuration);

        using ServiceProvider serviceProvider =
            services.BuildServiceProvider();

        // Act
        Action action =
            () => serviceProvider
                .GetRequiredService<IPaymentGateway>();

        // Assert
        OptionsValidationException exception =
            Assert.Throws<OptionsValidationException>(action);

        Assert.Contains(
            "Payments:Stripe:WebhookSecret is required",
            exception.Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public void AddPayments_WithFakeProvider_ShouldNotRequireStripeConfiguration()
    {
        // Arrange
        IConfiguration configuration =
            CreateConfiguration(
                new Dictionary<string, string?>
                {
                    ["Payments:Provider"] = "Fake"
                });

        var services =
            new ServiceCollection();

        // Act
        services.AddPayments(configuration);

        using ServiceProvider serviceProvider =
            services.BuildServiceProvider();

        IPaymentGateway gateway =
            serviceProvider.GetRequiredService<IPaymentGateway>();

        // Assert
        Assert.IsType<FakePaymentGateway>(gateway);
    }

    private static IConfiguration CreateConfiguration(
        IDictionary<string, string?> values)
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
    }
}
