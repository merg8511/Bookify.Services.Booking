using System.Net;
using System.Text.RegularExpressions;
using Bookify.Services.Booking.Integration.Tests.Infrastructure;

namespace Bookify.Services.Booking.Integration.Tests.Endpoints.OpenApi;

[Collection(BookingApiTestFixture.Name)]
[Trait("Category", "Integration")]
[Trait("Category", "Contract")]
[Trait("Category", "Security")]
public sealed class ScalarApiReferenceTests
{
    private const string ScalarPath = "/scalar/";
    private readonly BookingApiFactory _factory;

    public ScalarApiReferenceTests(BookingApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Scalar_ShouldServeHtmlInTesting()
    {
        CancellationToken cancellationToken =
            TestContext.Current.CancellationToken;

        using HttpResponseMessage response = await _factory.Client.GetAsync(
            ScalarPath,
            cancellationToken);

        ITestOutputHelper output = TestContext.Current.TestOutputHelper!;

        output.WriteLine($"StatusCode: {(int)response.StatusCode} {response.StatusCode}");
        output.WriteLine($"Location: {response.Headers.Location?.ToString() ?? "(none)"}");
        output.WriteLine($"RequestUri: {response.RequestMessage?.RequestUri}");

        Assert.True(
            response.StatusCode == HttpStatusCode.OK,
            $"Expected 200 OK, but received {(int)response.StatusCode} " +
            $"{response.StatusCode}. " +
            $"Location: {response.Headers.Location?.ToString() ?? "(none)"}");
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);

        string html = await response.Content.ReadAsStringAsync(cancellationToken);

        Assert.Contains("Bookify | Booking API", html, StringComparison.Ordinal);
        Assert.Contains("openapi", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(
            BookingApiFactory.StripeWebhookSecret,
            html,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task Scalar_ShouldUseFreshCspNonceOnEveryRequest()
    {
        CancellationToken cancellationToken =
            TestContext.Current.CancellationToken;

        using HttpResponseMessage first = await _factory.Client.GetAsync(
            ScalarPath,
            cancellationToken);

        using HttpResponseMessage second = await _factory.Client.GetAsync(
            ScalarPath,
            cancellationToken);

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);

        string firstPolicy = GetCsp(first);
        string secondPolicy = GetCsp(second);

        string firstNonce = ExtractNonce(firstPolicy);
        string secondNonce = ExtractNonce(secondPolicy);

        Assert.NotEqual(firstNonce, secondNonce);
        Assert.DoesNotContain("'unsafe-eval'", firstPolicy, StringComparison.Ordinal);
        Assert.DoesNotContain(
            "'unsafe-inline'",
            GetDirective(firstPolicy, "script-src"),
            StringComparison.Ordinal);

        Assert.Contains("connect-src 'self'", firstPolicy, StringComparison.Ordinal);
        Assert.Contains("frame-ancestors 'none'", firstPolicy, StringComparison.Ordinal);
        Assert.Contains("base-uri 'none'", firstPolicy, StringComparison.Ordinal);
        Assert.True(first.Headers.CacheControl?.NoStore == true);

        string firstHtml = await first.Content.ReadAsStringAsync(cancellationToken);
        Assert.Contains(firstNonce, firstHtml, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Health_ShouldKeepTheExistingStrictCsp()
    {
        CancellationToken cancellationToken =
            TestContext.Current.CancellationToken;

        using HttpResponseMessage response = await _factory.Client.GetAsync(
            "/health",
            cancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(
            "default-src 'none'; frame-ancestors 'none'; base-uri 'none'",
            GetCsp(response));
    }

    private static string GetCsp(HttpResponseMessage response)
    {
        Assert.True(response.Headers.TryGetValues(
            "Content-Security-Policy",
            out IEnumerable<string>? policies));

        return Assert.Single(policies!);
    }

    private static string ExtractNonce(string csp)
    {
        Match match = Regex.Match(
            csp,
            @"(?:^|;\s*)script-src\s+[^;]*'nonce-([^']+)'",
            RegexOptions.CultureInvariant);

        Assert.True(match.Success, "Scalar must provide a nonce-based script-src directive.");
        return match.Groups[1].Value;
    }

    private static string GetDirective(string csp, string directiveName)
    {
        string? directive = csp.Split(';', StringSplitOptions.TrimEntries)
            .FirstOrDefault(value => value.StartsWith(
                directiveName + " ",
                StringComparison.Ordinal));

        return Assert.IsType<string>(directive);
    }
}
