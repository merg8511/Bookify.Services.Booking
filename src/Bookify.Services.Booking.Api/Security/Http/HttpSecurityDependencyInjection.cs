using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;
using System.Net;

namespace Bookify.Services.Booking.Api.Security.Http;

internal static class HttpSecurityDependencyInjection
{
    public static IServiceCollection AddBookifyHttpSecurity(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddSingleton<
            IValidateOptions<HttpSecurityOptions>,
            HttpSecurityOptionsValidator>();

        services
            .AddOptions<HttpSecurityOptions>()
            .Bind(configuration.GetSection(HttpSecurityOptions.SectionName))
            .ValidateOnStart();

        services.AddCors();

        services.AddSingleton<
             IConfigureOptions<CorsOptions>,
             BookifyCorsOptionsSetup>();

        services.AddOptions<ForwardedHeadersOptions>();

        services.AddSingleton<
            IConfigureOptions<ForwardedHeadersOptions>,
            BookifyForwardedHeadersOptionsSetup>();

        services.AddHttpsRedirection(
            options =>
            {
                options.RedirectStatusCode = StatusCodes.Status307TemporaryRedirect;
            });

        services.AddHsts(
            options =>
            {
                options.MaxAge = TimeSpan.FromDays(365);
                options.IncludeSubDomains = false;
                options.Preload = false;
            });

        return services;
    }
}

internal sealed class BookifyCorsOptionsSetup : IConfigureOptions<CorsOptions>
{
    private readonly HttpSecurityOptions _options;

    public BookifyCorsOptionsSetup(IOptions<HttpSecurityOptions> options)
    {
        _options = options?.Value ??
            throw new ArgumentNullException(nameof(options));
    }

    public void Configure(CorsOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        options.AddPolicy(
            BookifyCorsPolicies.TrustedFrontends,
            policy =>
        {
            if (_options.TrustedFrontendOrigins.Length == 0)
            {
                policy.SetIsOriginAllowed(_ => false);
            }
            else
            {
                policy.WithOrigins(_options.TrustedFrontendOrigins);
            }

            policy
                .AllowAnyMethod()
                .AllowAnyHeader()
                .WithExposedHeaders(
                    BookingGuestAccessHeader.Name,
                    HeaderNames.Location,
                    HeaderNames.RetryAfter)
                .SetPreflightMaxAge(TimeSpan.FromMinutes(10));
        });
    }
}

internal sealed class BookifyForwardedHeadersOptionsSetup : IConfigureOptions<ForwardedHeadersOptions>
{
    private readonly HttpSecurityOptions _options;

    public BookifyForwardedHeadersOptionsSetup(IOptions<HttpSecurityOptions> options)
    {
        _options = options?.Value ??
            throw new ArgumentNullException(nameof(options));
    }

    public void Configure(ForwardedHeadersOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
        options.ForwardLimit = _options.ForwardedHeaders.ForwardLimit;
        options.RequireHeaderSymmetry = true;

        options.KnownProxies.Clear();
        options.KnownIPNetworks.Clear();

        foreach (string proxy in _options.ForwardedHeaders.KnownProxies)
        {
            if (IPAddress.TryParse(proxy, out IPAddress? address))
            {
                options.KnownProxies.Add(address);
            }
        }

        foreach (string network in _options.ForwardedHeaders.KnownNetworks)
        {
            if (System.Net.IPNetwork.TryParse(network, out System.Net.IPNetwork parsedNetwork))
            {
                options.KnownIPNetworks.Add(parsedNetwork);
            }
        }
    }
}
