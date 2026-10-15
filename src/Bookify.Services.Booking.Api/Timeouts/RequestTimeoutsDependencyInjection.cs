using Microsoft.AspNetCore.Http.Timeouts;
using Microsoft.IdentityModel.Tokens;

namespace Bookify.Services.Booking.Api.Timeouts;

internal static class RequestTimeoutsDependencyInjection
{
    public static IServiceCollection AddBookifyRequestTimeouts(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        IConfigurationSection section = configuration.GetSection(BookifyRequestTimeoutOptions.SectionName);

        services
            .AddOptions<BookifyRequestTimeoutOptions>()
            .Bind(section)
            .Validate(
                AreTimeoutsValid,
                "RequestTimeouts values must be greater than zero and no greater than five minutes.")
            .ValidateOnStart();

        var configuredOptions = new BookifyRequestTimeoutOptions();

        section.Bind(configuredOptions);

        services.AddRequestTimeouts(options =>
        {
            options.DefaultPolicy = CreatePolicy(configuredOptions.Default);
            options.AddPolicy(
                BookifyRequestTimeoutPolicies.PaymentInitiation, CreatePolicy(configuredOptions.PaymentInitiation));

            options.AddPolicy(
                BookifyRequestTimeoutPolicies.StripeWebhook, CreatePolicy(configuredOptions.StripeWebhook));
        });

        return services;
    }

    private static RequestTimeoutPolicy CreatePolicy(TimeSpan timeout)
    {
        return new RequestTimeoutPolicy
        {
            Timeout = timeout,
            TimeoutStatusCode = StatusCodes.Status504GatewayTimeout,
            WriteTimeoutResponse = RequestTimeoutProblemWriter.WriteAsync
        };
    }

    private static bool AreTimeoutsValid(BookifyRequestTimeoutOptions options)
    {
        return IsValid(options.Default) &&
               IsValid(options.PaymentInitiation) &&
               IsValid(options.StripeWebhook);

    }

    private static bool IsValid(TimeSpan timeout)
    {
        return timeout > TimeSpan.Zero &&
               timeout <= TimeSpan.FromMinutes(5);
    }
}
