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

        var rateLimitOptions = new BookifyRateLimitingOptions();

        configuration
            .GetSection(BookifyRateLimitingOptions.SectionName)
            .Bind(rateLimitOptions);

        services.AddRateLimiter(
            options =>
            {
                options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
                options.OnRejected = RateLimitingProblemWriter.WriteAsync;

                options.AddPolicy<string>(BookifyRateLimitPolicies.PublicReads, httpContext =>
                    CreateFixedWindowPartition(
                        RateLimitPartitionKeyResolver.ResolveIdendityOrIp(httpContext),
                        rateLimitOptions.PublicReads));

                options.AddPolicy<string>(BookifyRateLimitPolicies.BookingCreation, httpContext =>
                    CreateFixedWindowPartition(
                        RateLimitPartitionKeyResolver.ResolveIdendityOrIp(httpContext),
                        rateLimitOptions.BookingCreation));

                options.AddPolicy<string>(BookifyRateLimitPolicies.PaymentInitiation, httpContext =>
                    CreateFixedWindowPartition(
                        RateLimitPartitionKeyResolver.ResolveBookingActorOrIp(httpContext),
                        rateLimitOptions.PaymentInitiation));

                options.AddPolicy<string>(BookifyRateLimitPolicies.LoginFacing, httpContext =>
                    CreateFixedWindowPartition(
                        RateLimitPartitionKeyResolver.ResolveIp(httpContext),
                        rateLimitOptions.LoginFacing));

                options.AddPolicy<string>(BookifyRateLimitPolicies.Webhook, httpContext =>
                    CreateFixedWindowPartition(
                        RateLimitPartitionKeyResolver.ResolveIp(httpContext),
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
}
