using Bookify.Services.Booking.Api.Security.Authorization;
using Bookify.Services.Booking.Application.Abstractions.Security;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

namespace Bookify.Services.Booking.Api.Security;

internal static class IdentityDependencyInjection
{
    public static IServiceCollection AddIdentityAuthentication(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(environment);

        IConfigurationSection identitySection =
            configuration.GetSection(IdentityOptions.SectionName);

        string environmentName = configuration["ASPNETCORE_ENVIRONMENT"] ??
                                 configuration["DOTNET_ENVIRONMENT"] ??
                                 Environments.Production;

        bool isDevelopment = environment.IsDevelopment();

        services
            .AddOptions<IdentityOptions>()
            .Bind(identitySection)
            .Validate(
                options => IsValidAuthority(options.Authority, isDevelopment),
                "Identity:Authority must be a valid HTTPS authority URI, " +
                "or an HTTP loopback authority in Development.")
            .Validate(
                options => !string.IsNullOrWhiteSpace(options.Audience),
                "Identity:Audience must contain a non-empty API audience.")
            .ValidateOnStart();

        IdentityOptions identityOptions = identitySection.Get<IdentityOptions>() ?? new IdentityOptions();

        string authority = identityOptions.Authority.Trim();
        string audience = identityOptions.Audience.Trim();

        bool requireHttpsMetadata = !IsDevelopmentHttpLoopback(authority, isDevelopment);

        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUser, HttpContextCurrentUser>();

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.Authority = authority;
                options.Audience = audience;
                options.RequireHttpsMetadata = requireHttpsMetadata;
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

    private static bool IsValidAuthority(string? authority, bool isDevelopment)
    {
        string value = authority?.Trim() ?? string.Empty;

        if (!Uri.TryCreate(value, UriKind.Absolute, out Uri? uri))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(uri.Host) ||
            !string.IsNullOrEmpty(uri.UserInfo) ||
            !string.IsNullOrEmpty(uri.Query) ||
            !string.IsNullOrEmpty(uri.Fragment))
        {
            return false;
        }

        return uri.Scheme == Uri.UriSchemeHttps || IsDevelopmentHttpLoopback(value, isDevelopment);
    }

    private static bool IsDevelopmentHttpLoopback(string authority, bool isDevelopment)
    {
        if (!isDevelopment || !Uri.TryCreate(authority, UriKind.Absolute, out Uri? uri))
        {
            return false;
        }

        return uri.Scheme == Uri.UriSchemeHttp &&
               uri.IsLoopback &&
               string.IsNullOrEmpty(uri.UserInfo) &&
               string.IsNullOrEmpty(uri.Query) &&
               string.IsNullOrEmpty(uri.Fragment);
    }
}
