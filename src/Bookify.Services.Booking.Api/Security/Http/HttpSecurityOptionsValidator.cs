using Microsoft.Extensions.Options;
using System.Net;

namespace Bookify.Services.Booking.Api.Security.Http;

internal sealed class HttpSecurityOptionsValidator : IValidateOptions<HttpSecurityOptions>
{
    public ValidateOptionsResult Validate(string? name, HttpSecurityOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var failures = new List<string>();

        ValidateOrigins(options.TrustedFrontendOrigins, failures);

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(string.Join(" ", failures));
    }

    private static void ValidateOrigins(
        IEnumerable<string>? origins,
        List<string> failures)
    {
        if (origins is null)
        {
            failures.Add("Trusted frontend origins cannot be null.");
            return;
        }

        var uniqueOrigins = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (string origin in origins)
        {
            if (!IsValidOrigin(origin))
            {
                failures.Add($"Trusted frontend origin '{origin}' must be an absolute HTTP or HTTPS origin " +
                    $"without path, credentials, query, fragment or wildcard.");

                continue;
            }

            if (!uniqueOrigins.Add(origin))
            {
                failures.Add($"Trusted frontend origin '{origin}' is configured more than once.");
            }
        }
    }

    private static void ValidateForwardedHeaders(
        ForwardedHeadersSecurityOptions? options,
        ICollection<string> failures)
    {
        if (options is null)
        {
            failures.Add("Forwarded headers configuration is required.");

            return;
        }

        if (options.ForwardLimit is < 1 or > 10)
        {
            failures.Add("ForwardedHeaders:ForwardLimit must be between 1 and 10.");
        }

        foreach (string proxy in options.KnownProxies ?? [])
        {
            if (!IPAddress.TryParse(proxy, out IPAddress? address) ||
                address.Equals(IPAddress.Any) ||
                address.Equals(IPAddress.IPv6Any))
            {
                failures.Add($"Know proxy '{proxy}' must contain a specific valid IP address.");
            }
        }

        foreach (string network in options.KnownNetworks ?? [])
        {
            if (!IPNetwork.TryParse(network, out IPNetwork parsedNetwork) ||
                parsedNetwork.PrefixLength == 0)
            {
                failures.Add($"Know network '{network}' must contain a valid, restricted CIDR network.");
            }
        }
    }

    private static bool IsValidOrigin(string? origin)
    {
        if (string.IsNullOrWhiteSpace(origin) ||
            origin.Contains('*', StringComparison.Ordinal))
        {
            return false;
        }

        if (!Uri.TryCreate(origin, UriKind.Absolute, out Uri? uri))
        {
            return false;
        }

        if (uri.Scheme != Uri.UriSchemeHttp &&
            uri.Scheme != Uri.UriSchemeHttps)
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

        string expectedOrigin = uri.GetLeftPart(UriPartial.Authority);

        return string.Equals(origin, expectedOrigin, StringComparison.OrdinalIgnoreCase);
    }
}
