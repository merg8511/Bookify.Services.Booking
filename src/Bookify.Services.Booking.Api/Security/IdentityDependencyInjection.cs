using Bookify.Services.Booking.Api.Security.Authorization;
using Bookify.Services.Booking.Application.Abstractions.Security;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

namespace Bookify.Services.Booking.Api.Security;

internal static class IdentityDependencyInjection
{
    public static IServiceCollection AddIdentityAuthentication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        IdentityOptions identityOptions = configuration
            .GetSection(IdentityOptions.SectionName)
            .Get<IdentityOptions>() ??
            throw new InvalidOperationException("Identity configuration is missing. " +
                "Configure 'Identity:Authority' and 'Identity:Audience'.");

        string authority = identityOptions.Authority.Trim();
        string audience = identityOptions.Audience.Trim();

        if (!Uri.TryCreate(authority, UriKind.Absolute, out Uri? authorityUri) ||
            authorityUri.Scheme != Uri.UriSchemeHttps ||
            string.IsNullOrWhiteSpace(authorityUri.Host) ||
            !string.IsNullOrEmpty(authorityUri.UserInfo) ||
            !string.IsNullOrEmpty(authorityUri.Query) ||
            !string.IsNullOrEmpty(authorityUri.Fragment))
        {
            throw new InvalidOperationException("Configuration 'Identity:Authority' " +
                "must contain a valid HTTPS authority URI without credentials, query or fragment.");
        }

        if (string.IsNullOrWhiteSpace(audience))
        {
            throw new InvalidOperationException("Configuration 'Identity:Audience' " +
                "must contain a non-empty API audience.");
        }

        services.AddHttpContextAccessor();

        services.AddScoped<ICurrentUser, HttpContextCurrentUser>();

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.Authority = authority;
                options.Audience = audience;
                options.RequireHttpsMetadata = true;
                options.MapInboundClaims = false;
                options.IncludeErrorDetails = false;

                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    RequireSignedTokens = true,
                    RequireExpirationTime = true,
                    NameClaimType = BookifyClaimTypes.Subject,
                    RoleClaimType = BookifyRoles.ClaimType,
                    ClockSkew = TimeSpan.FromMinutes(1)
                };

                options.Events = new JwtBearerEvents
                {
                    OnTokenValidated = context =>
                    {
                        string? subject = context.Principal?.FindFirst(BookifyClaimTypes.Subject)?.Value;

                        if (string.IsNullOrWhiteSpace(subject))
                        {
                            context.Fail("The access token must contain a non-empty 'sub' claim.");
                        }

                        return Task.CompletedTask;
                    }
                };
            });

        services.AddBookifyAuthorization();

        return services;
    }
}
