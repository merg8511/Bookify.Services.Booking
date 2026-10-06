using Bookify.Services.Booking.Integration.Tests.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.HttpsPolicy;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System.Net;

namespace Bookify.Services.Booking.Integration.Tests.Security;

[Collection(BookingApiTestFixture.Name)]
[Trait("Category", "Integration")]
public sealed class HttpSecurityIntegrationTests
{
    private readonly BookingApiFactory _factory;

    public HttpSecurityIntegrationTests(
        BookingApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task TrustedOrigin_ShouldReceiveCorsHeaders()
    {
        // Arrange
        using var request =
            new HttpRequestMessage(
                HttpMethod.Get,
                "/api/v1/properties");

        request.Headers.TryAddWithoutValidation(
            "Origin",
            BookingApiFactory.TrustedFrontendOrigin);

        // Act
        using HttpResponseMessage response =
            await _factory.Client.SendAsync(
                request,
                TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(
            HttpStatusCode.OK,
            response.StatusCode);

        Assert.True(
            response.Headers.TryGetValues(
                "Access-Control-Allow-Origin",
                out IEnumerable<string>? allowedOrigins));

        Assert.Contains(
            BookingApiFactory.TrustedFrontendOrigin,
            allowedOrigins);

        Assert.True(
            response.Headers.TryGetValues(
                "Access-Control-Expose-Headers",
                out IEnumerable<string>? exposedHeaders));

        string exposed =
            string.Join(
                ",",
                exposedHeaders);

        Assert.Contains(
            "Booking-Guest-Token",
            exposed,
            StringComparison.OrdinalIgnoreCase);

        Assert.Contains(
            "Location",
            exposed,
            StringComparison.OrdinalIgnoreCase);

        Assert.Contains(
            "Retry-After",
            exposed,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UntrustedOrigin_ShouldNotReceiveCorsPermission()
    {
        // Arrange
        using var request =
            new HttpRequestMessage(
                HttpMethod.Get,
                "/api/v1/properties");

        request.Headers.TryAddWithoutValidation(
            "Origin",
            "https://attacker.example");

        // Act
        using HttpResponseMessage response =
            await _factory.Client.SendAsync(
                request,
                TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(
            HttpStatusCode.OK,
            response.StatusCode);

        Assert.False(
            response.Headers.Contains(
                "Access-Control-Allow-Origin"));
    }

    [Fact]
    public async Task TrustedOrigin_Preflight_ShouldSucceed()
    {
        // Arrange
        using var request =
            new HttpRequestMessage(
                HttpMethod.Options,
                "/api/v1/bookings");

        request.Headers.TryAddWithoutValidation(
            "Origin",
            BookingApiFactory.TrustedFrontendOrigin);

        request.Headers.TryAddWithoutValidation(
            "Access-Control-Request-Method",
            "POST");

        request.Headers.TryAddWithoutValidation(
            "Access-Control-Request-Headers",
            "authorization,content-type," +
            "idempotency-key,booking-guest-token");

        // Act
        using HttpResponseMessage response =
            await _factory.Client.SendAsync(
                request,
                TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(
            HttpStatusCode.NoContent,
            response.StatusCode);

        Assert.True(
            response.Headers.Contains(
                "Access-Control-Allow-Origin"));

        Assert.True(
            response.Headers.Contains(
                "Access-Control-Allow-Methods"));

        Assert.True(
            response.Headers.Contains(
                "Access-Control-Allow-Headers"));
    }

    [Fact]
    public async Task Responses_ShouldContainSecurityHeaders()
    {
        // Act
        using HttpResponseMessage response =
            await _factory.Client.GetAsync(
                "/health",
                TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(
            HttpStatusCode.OK,
            response.StatusCode);

        AssertHeader(
            response,
            "X-Content-Type-Options",
            "nosniff");

        AssertHeader(
            response,
            "X-Frame-Options",
            "DENY");

        AssertHeader(
            response,
            "Referrer-Policy",
            "no-referrer");

        AssertHeader(
            response,
            "Permissions-Policy",
            "camera=(), microphone=(), geolocation=()");

        Assert.True(
            response.Headers.Contains(
                "Content-Security-Policy"));
    }

    [Fact]
    public void ForwardedHeaders_ShouldTrustOnlyConfiguredSources()
    {
        // Arrange
        ForwardedHeadersOptions options =
            _factory.Services
                .GetRequiredService<
                    IOptions<ForwardedHeadersOptions>>()
                .Value;

        // Assert
        Assert.True(
            options.ForwardedHeaders.HasFlag(
                ForwardedHeaders.XForwardedFor));

        Assert.True(
            options.ForwardedHeaders.HasFlag(
                ForwardedHeaders.XForwardedProto));

        Assert.Equal(
            1,
            options.ForwardLimit);

        Assert.True(
            options.RequireHeaderSymmetry);

        Assert.Contains(
            IPAddress.Parse("127.0.0.1"),
            options.KnownProxies);
    }

    [Fact]
    public void HttpsAndHsts_ShouldUseSecureConfiguration()
    {
        // Arrange
        HttpsRedirectionOptions httpsOptions =
            _factory.Services
                .GetRequiredService<
                    IOptions<HttpsRedirectionOptions>>()
                .Value;

        HstsOptions hstsOptions =
            _factory.Services
                .GetRequiredService<
                    IOptions<HstsOptions>>()
                .Value;

        // Assert
        Assert.Equal(
            StatusCodes.Status307TemporaryRedirect,
            httpsOptions.RedirectStatusCode);

        Assert.Equal(
            TimeSpan.FromDays(365),
            hstsOptions.MaxAge);

        Assert.False(
            hstsOptions.Preload);
    }

    [Fact]
    public void Startup_WithWildcardTrustedOrigin_ShouldFailValidation()
    {
        // Arrange
        using WebApplicationFactory<Program> invalidFactory =
            _factory.WithWebHostBuilder(
                builder =>
                {
                    builder.UseSetting(
                        "ConnectionStrings:Database",
                        _factory.DatabaseConnectionString);

                    builder.UseSetting(
                        "HttpSecurity:TrustedFrontendOrigins:0",
                        "*");
                });

        // Act
        Exception exception =
            Assert.ThrowsAny<Exception>(
                () =>
                {
                    using HttpClient client =
                        invalidFactory.CreateClient();
                });

        // Assert
        Assert.Contains(
            "Trusted frontend origin",
            exception.ToString(),
            StringComparison.Ordinal);
    }

    private static void AssertHeader(
        HttpResponseMessage response,
        string headerName,
        string expectedValue)
    {
        Assert.True(
            response.Headers.TryGetValues(
                headerName,
                out IEnumerable<string>? values));

        Assert.Contains(
            expectedValue,
            values);
    }
}
