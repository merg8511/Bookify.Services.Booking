using Bookify.Services.Booking.Application.Abstractions.Payments;
using Bookify.Services.Booking.Infrastructure.Payments;
using Bookify.Services.Booking.Infrastructure.Payments.Fake;
using Bookify.Services.Booking.Infrastructure.Payments.Stripe;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Bookify.Services.Booking.IntegrationTests.Payments;

public sealed class PaymentsDependencyInjectionTests
{
    [Fact]
    public void AddPayments_WithFakeProvider_ShouldResolveFakeGateway()
    {
        // ARRANGE
        IConfiguration configuration =
            CreateConfiguration(
                new Dictionary<string, string?>
                {
                    ["Payments:Provider"] =
                        "Fake"
                });

        var services =
            new ServiceCollection();

        // ACT
        services.AddPayments(
            configuration);

        using ServiceProvider
            serviceProvider =
                services.BuildServiceProvider();

        IPaymentGateway gateway =
            serviceProvider
                .GetRequiredService<
                    IPaymentGateway>();

        // ASSERT
        Assert.IsType<
            FakePaymentGateway>(
                gateway);
    }

    [Fact]
    public void AddPayments_WithStripeProvider_ShouldResolveStripeGateway()
    {
        // ARRANGE
        IConfiguration configuration =
            CreateConfiguration(
                new Dictionary<string, string?>
                {
                    ["Payments:Provider"] =
                        "Stripe",

                    ["Payments:Stripe:SecretKey"] =
                        "sk_test_bookify"
                });

        var services =
            new ServiceCollection();

        // ACT
        services.AddPayments(
            configuration);

        using ServiceProvider
            serviceProvider =
                services.BuildServiceProvider();

        IPaymentGateway gateway =
            serviceProvider
                .GetRequiredService<
                    IPaymentGateway>();

        // ASSERT
        Assert.IsType<
            StripePaymentGateway>(
                gateway);
    }

    [Theory]
    [InlineData("")]
    [InlineData("Unknown")]
    [InlineData("PayPal")]
    public void AddPayments_WithInvalidProvider_ShouldThrow(
        string provider)
    {
        // ARRANGE
        IConfiguration configuration =
            CreateConfiguration(
                new Dictionary<string, string?>
                {
                    ["Payments:Provider"] =
                        provider
                });

        var services =
            new ServiceCollection();

        // ACT
        Action action =
            () =>
                services.AddPayments(
                    configuration);

        // ASSERT
        InvalidOperationException exception =
            Assert.Throws<
                InvalidOperationException>(
                    action);

        Assert.Contains(
            "Payment provider",
            exception.Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public void AddPayments_WithStripeProviderAndMissingSecretKey_ShouldThrow()
    {
        // ARRANGE
        IConfiguration configuration =
            CreateConfiguration(
                new Dictionary<string, string?>
                {
                    ["Payments:Provider"] =
                        "Stripe"
                });

        var services =
            new ServiceCollection();

        // ACT
        Action action =
            () =>
                services.AddPayments(
                    configuration);

        // ASSERT
        InvalidOperationException exception =
            Assert.Throws<
                InvalidOperationException>(
                    action);

        Assert.Contains(
            "Stripe secret key is not configured",
            exception.Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public void AddPayments_WithFakeProvider_ShouldNotRequireStripeSecretKey()
    {
        // ARRANGE
        IConfiguration configuration =
            CreateConfiguration(
                new Dictionary<string, string?>
                {
                    ["Payments:Provider"] =
                        "Fake"
                });

        var services =
            new ServiceCollection();

        // ACT
        services.AddPayments(
            configuration);

        using ServiceProvider
            serviceProvider =
                services.BuildServiceProvider();

        IPaymentGateway gateway =
            serviceProvider
                .GetRequiredService<
                    IPaymentGateway>();

        // ASSERT
        Assert.IsType<
            FakePaymentGateway>(
                gateway);
    }

    private static IConfiguration
        CreateConfiguration(
            IDictionary<string, string?>
                values)
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(
                values)
            .Build();
    }
}
