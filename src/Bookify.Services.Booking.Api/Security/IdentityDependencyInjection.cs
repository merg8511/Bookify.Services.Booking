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

        IConfigurationSection identitySection = configuration.GetSection(IdentityOptions.SectionName);

        services
            .AddOptions<IdentityOptions>()
            .Bind(identitySection)
            .Validate(options =>
                IsValidAuthority(options.Authority),
                "Identity:Authority must contain a valid HTTPS authority URI without" +
                " credentials, query or fragment.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.Audience),
                "Identity:Audience must contain a non-empty API audience.")
            .ValidateOnStart();

        IdentityOptions identityOptions = identitySection.Get<IdentityOptions>() ?? new IdentityOptions();

        string authority = identityOptions.Authority.Trim();
        string audience = identityOptions.Audience.Trim();

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

    private static bool IsValidAuthority(string? authority)
    {
        string value = authority?.Trim() ?? string.Empty;

        return Uri.TryCreate(value, UriKind.Absolute, out Uri? authorityUri) &&
            authorityUri.Scheme == Uri.UriSchemeHttps &&
            !string.IsNullOrWhiteSpace(authorityUri.Host) &&
            string.IsNullOrEmpty(authorityUri.UserInfo) &&
            string.IsNullOrEmpty(authorityUri.Query) &&
            string.IsNullOrEmpty(authorityUri.Fragment);
    }
}
