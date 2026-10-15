using Bookify.Services.Booking.Application.Abstractions.Payments;
using Bookify.Services.Booking.Infrastructure.Payments.Fake;
using Bookify.Services.Booking.Infrastructure.Payments.Stripe;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
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

        services
            .AddOptions<PaymentOptions>()
            .Bind(configuration.GetSection(PaymentOptions.SectionName))
            .Validate(options =>
                Enum.TryParse<PaymentProvider>(options.Provider.Trim(), ignoreCase: true, out _),
                "Payments:Provider must contain a supported payment provider.")
            .ValidateOnStart();

        services
            .AddOptions<StripePaymentOptions>()
            .Bind(configuration.GetSection(StripePaymentOptions.SectionName))
            .Validate(options =>
                options.WebhookToleranceSeconds > 0,
                "Payments:Stripe:WebhookToleranceSeconds must be greater than zero.")
            .Validate(options =>
                options.HttpTimeoutSeconds is >= 1 and <= 60,
                "Payments:Stripe:HttpTimeoutSeconds must be between 1 and 60.")
            .Validate(options =>
                options.MaxTransientRetries is >= 0 and <= 2,
                "Payments:Stripe:MaxTransientRetries must be between 0 and 2.")
            .Validate(options =>
                options.RetryBaseDelayMilliseconds is >= 1 and <= 1000,
                "Payments:Stripe:RetryBaseDelayMilliseconds must be between 1 and 1000.")
            .Validate(options =>
                provider != PaymentProvider.Stripe || !string.IsNullOrWhiteSpace(options.SecretKey),
                "Payments:Stripe:SecretKey is required when Stripe is the configured provider.")
            .Validate(options =>
                provider != PaymentProvider.Stripe || !string.IsNullOrWhiteSpace(options.WebhookSecret),
                "Payments:Stripe:WebhookSecret is required when Stripe is the configured provider.")
            .ValidateOnStart();

        AddStripeWebhookSignatureVerification(services);

        return provider switch
        {
            PaymentProvider.Fake => AddFakePaymentGateway(services),
            PaymentProvider.Stripe => AddStripePaymentGateway(services),
            _ => throw new InvalidOperationException($"Payment provider '{provider}' is not supported.")
        };
    }

    private static void AddStripeWebhookSignatureVerification(IServiceCollection services)
    {
        services.AddSingleton<IStripeWebhookSignatureVerifier>(
            serviceProvider =>
            {
                StripePaymentOptions options = serviceProvider
                    .GetRequiredService<IOptions<StripePaymentOptions>>()
                    .Value;

                return new StripeWebhookSignatureVerifier(
                    options.WebhookSecret.Trim(),
                    options.WebhookToleranceSeconds);
            });
    }

    private static IServiceCollection AddFakePaymentGateway(IServiceCollection services)
    {
        services.AddSingleton<FakePaymentGateway>();

        services.AddSingleton<IPaymentGateway>(
                serviceProvider => serviceProvider.GetRequiredService<FakePaymentGateway>());

        return services;
    }

    private static IServiceCollection AddStripePaymentGateway(
    IServiceCollection services)
    {
        services.AddSingleton<StripeClientTransport>(serviceProvider =>
            {
                StripePaymentOptions options = serviceProvider.GetRequiredService<
                            IOptions<StripePaymentOptions>>().Value;

                return new StripeClientTransport(options.SecretKey.Trim(), TimeSpan.FromSeconds(options.HttpTimeoutSeconds));
            });

        services.AddSingleton<IStripeClient>(serviceProvider => serviceProvider
            .GetRequiredService<StripeClientTransport>().Client);

        services.AddSingleton<PaymentIntentService>();

        services.AddSingleton<StripePaymentGateway>(
            serviceProvider =>
            {
                StripePaymentOptions options = serviceProvider
                    .GetRequiredService<IOptions<StripePaymentOptions>>()
                    .Value;

                return new StripePaymentGateway(
                    serviceProvider.GetRequiredService<PaymentIntentService>(),
                    options.MaxTransientRetries,
                    options.RetryBaseDelayMilliseconds);
            });

        services.AddSingleton<IPaymentGateway>(serviceProvider => serviceProvider.GetRequiredService<StripePaymentGateway>());

        return services;
    }

    private sealed class StripeClientTransport : IDisposable
    {
        private readonly HttpClient _httpCliente;
        public IStripeClient Client { get; }

        public StripeClientTransport(string secretKey, TimeSpan timeout)
        {
            var handler = new SocketsHttpHandler
            {
                PooledConnectionLifetime = TimeSpan.FromMinutes(5)
            };

            _httpCliente = new HttpClient(handler)
            {
                Timeout = timeout
            };

            var stripeHttpClient = new SystemNetHttpClient(
                httpClient: _httpCliente,
                maxNetworkRetries: 0);

            Client = new StripeClient(
                apiKey: secretKey,
                httpClient: stripeHttpClient);
        }
        public void Dispose()
        {
            _httpCliente.Dispose();
        }
    }
}


