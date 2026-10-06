using Bookify.Services.Booking.Api.Security;
using System.Threading.RateLimiting;

namespace Bookify.Services.Booking.Api.RateLimiting;

internal static class RateLimitingDependencyInjection
{
    public static IServiceCollection AddBookifyRateLimiting(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        IConfigurationSection section = configuration.GetSection(BookifyRateLimitingOptions.SectionName);

        services
            .AddOptions<BookifyRateLimitingOptions>()
            .Bind(section)
            .Validate(AreRulesValid, "Every rate limiting rule must ha a positive permit limit and window.")
            .ValidateOnStart();

        var rateLimitOptions = new BookifyRateLimitingOptions();
        section.Bind(rateLimitOptions);

        services.AddRateLimiter(
            options =>
            {
                options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
                options.OnRejected = RateLimitingProblemWriter.WriteAsync;

                options.AddPolicy<string>(BookifyRateLimitPolicies.PublicReads, httpContext =>
                    CreateFixedWindowPartition(
                        RequestActorKeyResolver.ResolveIdentityOrIp(httpContext),
                        rateLimitOptions.PublicReads));

                options.AddPolicy<string>(BookifyRateLimitPolicies.BookingCreation, httpContext =>
                    CreateFixedWindowPartition(
                        RequestActorKeyResolver.ResolveIdentityOrIp(httpContext),
                        rateLimitOptions.BookingCreation));

                options.AddPolicy<string>(BookifyRateLimitPolicies.PaymentInitiation, httpContext =>
                    CreateFixedWindowPartition(
                        RequestActorKeyResolver.ResolveBookingActorOrIp(httpContext),
                        rateLimitOptions.PaymentInitiation));

                options.AddPolicy<string>(BookifyRateLimitPolicies.LoginFacing, httpContext =>
                    CreateFixedWindowPartition(
                        RequestActorKeyResolver.ResolveIp(httpContext),
                        rateLimitOptions.LoginFacing));

                options.AddPolicy<string>(BookifyRateLimitPolicies.Webhook, httpContext =>
                    CreateFixedWindowPartition(
                        RequestActorKeyResolver.ResolveIp(httpContext),
                        rateLimitOptions.Webhook));
            });

        return services;
    }

    private static RateLimitPartition<string> CreateFixedWindowPartition(
        string partitionKey,
        RateLimitRuleOptions rule)
    {
        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey,
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = rule.PermitLimit,
                Window = rule.Window,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0,
                AutoReplenishment = false
            });
    }

    private static bool AreRulesValid(BookifyRateLimitingOptions options)
    {
        return
            IsRuleValid(options.PublicReads) &&
            IsRuleValid(options.BookingCreation) &&
            IsRuleValid(options.PaymentInitiation) &&
            IsRuleValid(options.LoginFacing) &&
            IsRuleValid(options.Webhook);

    }

    private static bool IsRuleValid(RateLimitRuleOptions? rule)
    {
        return
            rule is not null &&
            rule.PermitLimit > 0 &&
            rule.Window > TimeSpan.Zero;
    }
}
