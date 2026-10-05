using Bookify.Services.Booking.Application.Abstractions.Security;
using Microsoft.AspNetCore.Authorization;

namespace Bookify.Services.Booking.Api.Security.Authorization;

internal static class AuthorizationDependencyInjection
{
    public static IServiceCollection AddBookifyAuthorization(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddAuthorization(
            options =>
            {
                options.AddPolicy(BookifyAuthorizationPolicies.AdminOnly, policy =>
                {
                    policy.RequireAuthenticatedUser();
                    policy.RequireRole(BookifyRoles.Admin);
                });

                options.AddPolicy(BookifyAuthorizationPolicies.PropertyOwnerOrAdmin, policy =>
                {
                    policy.RequireAuthenticatedUser();
                    policy.Requirements.Add(new PropertyOwnerOrAdminRequirement());
                });

                options.AddPolicy(BookifyAuthorizationPolicies.BookingOwner, policy =>
                {
                    policy.RequireAuthenticatedUser();
                    policy.Requirements.Add(new BookingOwnerRequirement());
                });

                options.AddPolicy(BookifyAuthorizationPolicies.BookingGuestAccess, policy =>
                {
                    policy.Requirements.Add(new BookingGuestAccessRequirement());
                });

                options.AddPolicy(BookifyAuthorizationPolicies.CustomerOrGuestBookingAccess, policy =>
                {
                    policy.Requirements.Add(new CustomerOrGuestBookingAccessRequirement());
                });
            });

        services.AddScoped<BookingAuthorizationResolver>();
        services.AddScoped<BookingAccessEvaluator>();
        services.AddScoped<IAuthorizationHandler, PropertyOwnerOrAdminAuthorizationHandler>();
        services.AddScoped<IAuthorizationHandler, BookingOwnerAuthorizationHandler>();
        services.AddScoped<IAuthorizationHandler, BookingGuestAccessAuthorizationHandler>();
        services.AddScoped<IAuthorizationHandler, CustomerOrGuestBookingAccessAuthorizationHandler>();

        return services;
    }
}
