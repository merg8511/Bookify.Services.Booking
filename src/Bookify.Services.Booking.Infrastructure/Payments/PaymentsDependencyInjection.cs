using Bookify.Services.Booking.Application.Abstractions.Payments;
using Bookify.Services.Booking.Infrastructure.Payments.Fake;
using Bookify.Services.Booking.Infrastructure.Payments.Stripe;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Stripe;

namespace Bookify.Services.Booking.Infrastructure.Payments;

public static class PaymentsDependencyInjection
{
    public static IServiceCollection AddPayments(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        string configuredProvider = configuration[$"{PaymentOptions.SectionName}:Provider"]?.Trim() ?? string.Empty;

        if (!Enum.TryParse(configuredProvider, ignoreCase: true, out PaymentProvider provider))
        {
            throw new InvalidOperationException(
                $"Payment provider '{configuredProvider}' is invalid. " +
                $"Supported providers are: " +
                $"{PaymentProvider.Fake}, " +
                $"{PaymentProvider.Stripe}.");
        }

        return provider switch
        {
            PaymentProvider.Fake =>
                AddFakePaymentGateway(services),

            PaymentProvider.Stripe =>
                AddStripePaymentGateway(services, configuration),

            _ =>
                throw new InvalidOperationException($"Payment provider '{provider}' is not supported.")
        };
    }

    private static IServiceCollection AddFakePaymentGateway(IServiceCollection services)
    {
        services.AddSingleton<FakePaymentGateway>();

        services.AddSingleton<
            IPaymentGateway>(
                serviceProvider =>
                    serviceProvider.GetRequiredService<FakePaymentGateway>());

        return services;
    }

    private static IServiceCollection
        AddStripePaymentGateway(
            IServiceCollection services,
            IConfiguration configuration)
    {
        string secretKey = configuration["Payments:Stripe:SecretKey"]?
            .Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(secretKey))
        {
            throw new InvalidOperationException(
                "Stripe secret key is not configured. " +
                "Configure 'Payments:Stripe:SecretKey'.");
        }

        services.AddSingleton<IStripeClient>(new StripeClient(secretKey));

        services.AddSingleton<PaymentIntentService>();

        services.AddSingleton<StripePaymentGateway>();

        services.AddSingleton<IPaymentGateway>(
            serviceProvider =>
                serviceProvider.GetRequiredService<StripePaymentGateway>());

        return services;
    }
}
