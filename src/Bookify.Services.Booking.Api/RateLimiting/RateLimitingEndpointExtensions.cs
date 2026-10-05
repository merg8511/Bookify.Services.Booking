namespace Bookify.Services.Booking.Api.RateLimiting;

internal static class RateLimitingEndpointExtensions
{
    public static RouteHandlerBuilder RequirePublicReadRateLimit(
        this RouteHandlerBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder.RequireRateLimiting(BookifyRateLimitPolicies.PublicReads);
    }

    public static RouteHandlerBuilder RequireBookingCreationRateLimit(
        this RouteHandlerBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder.RequireRateLimiting(BookifyRateLimitPolicies.BookingCreation);
    }

    public static RouteHandlerBuilder RequirePaymentInitiationRateLimit(
        this RouteHandlerBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder.RequireRateLimiting(BookifyRateLimitPolicies.PaymentInitiation);
    }

    public static RouteHandlerBuilder RequireLoginFacingRateLimit(
        this RouteHandlerBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder.RequireRateLimiting(BookifyRateLimitPolicies.LoginFacing);
    }

    public static RouteHandlerBuilder RequireWebhookRateLimit(
        this RouteHandlerBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder.RequireRateLimiting(BookifyRateLimitPolicies.Webhook);
    }
}
