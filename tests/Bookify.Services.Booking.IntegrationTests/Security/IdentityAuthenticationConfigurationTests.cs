
using Bookify.Services.Booking.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System.Net;
using System.Security.Claims;

namespace Bookify.Services.Booking.IntegrationTests.Security;

[Collection(BookingApiTestFixture.Name)]
[Trait("Category", "Integration")]
public sealed class IdentityAuthenticationConfigurationTests
{
    private readonly BookingApiFactory _factory;

    public IdentityAuthenticationConfigurationTests(
        BookingApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Authentication_ShouldUseJwtBearerAsDefaultScheme()
    {
        // Arrange
        IAuthenticationSchemeProvider schemeProvider =
            _factory.Services.GetRequiredService<
                IAuthenticationSchemeProvider>();

        // Act
        AuthenticationScheme? scheme =
            await schemeProvider.GetDefaultAuthenticateSchemeAsync();

        // Assert
        Assert.NotNull(scheme);

        Assert.Equal(
            JwtBearerDefaults.AuthenticationScheme,
            scheme.Name);
    }

    [Fact]
    public void Authentication_ShouldUseConfiguredAuthorityAndAudience()
    {
        // Arrange
        JwtBearerOptions options = GetJwtBearerOptions();

        // Assert
        Assert.Equal(
            BookingApiFactory.IdentityAuthority,
            options.Authority);

        Assert.Equal(
            BookingApiFactory.IdentityAudience,
            options.Audience);
    }

    [Fact]
    public void Authentication_ShouldRequireSecureTokenValidation()
    {
        // Arrange
        JwtBearerOptions options = GetJwtBearerOptions();

        // Assert
        Assert.True(options.RequireHttpsMetadata);

        Assert.False(options.MapInboundClaims);

        Assert.False(options.IncludeErrorDetails);

        Assert.True(
            options.TokenValidationParameters.ValidateIssuer);

        Assert.True(
            options.TokenValidationParameters.ValidateAudience);

        Assert.True(
            options.TokenValidationParameters.ValidateLifetime);

        Assert.True(
            options.TokenValidationParameters.ValidateIssuerSigningKey);

        Assert.True(
            options.TokenValidationParameters.RequireSignedTokens);

        Assert.True(
            options.TokenValidationParameters.RequireExpirationTime);

        Assert.Equal(
            "sub",
            options.TokenValidationParameters.NameClaimType);
    }

    [Fact]
    public async Task TokenValidated_WithoutSubject_ShouldFail()
    {
        // Arrange
        JwtBearerOptions options = GetJwtBearerOptions();

        TokenValidatedContext context =
            CreateTokenValidatedContext(
                options,
                subject: null);

        // Act
        await options.Events.TokenValidated(context);

        // Assert
        Assert.NotNull(context.Result);

        Assert.NotNull(context.Result.Failure);
    }

    [Fact]
    public async Task TokenValidated_WithEmptySubject_ShouldFail()
    {
        // Arrange
        JwtBearerOptions options = GetJwtBearerOptions();

        TokenValidatedContext context =
            CreateTokenValidatedContext(
                options,
                subject: "   ");

        // Act
        await options.Events.TokenValidated(context);

        // Assert
        Assert.NotNull(context.Result);

        Assert.NotNull(context.Result.Failure);
    }

    [Fact]
    public async Task TokenValidated_WithSubject_ShouldNotFail()
    {
        // Arrange
        JwtBearerOptions options = GetJwtBearerOptions();

        TokenValidatedContext context =
            CreateTokenValidatedContext(
                options,
                subject: "customer-123");

        // Act
        await options.Events.TokenValidated(context);

        // Assert
        Assert.Null(context.Result?.Failure);

        Assert.Equal(
            "customer-123",
            context.Principal?.FindFirst("sub")?.Value);
    }

    [Fact]
    public async Task Health_WithoutAuthentication_ShouldReturnOk()
    {
        // Arrange
        CancellationToken cancellationToken =
            TestContext.Current.CancellationToken;

        // Act
        using HttpResponseMessage response =
            await _factory.Client.GetAsync(
                "/health",
                cancellationToken);

        // Assert
        Assert.Equal(
            HttpStatusCode.OK,
            response.StatusCode);
    }

    private JwtBearerOptions GetJwtBearerOptions()
    {
        IOptionsMonitor<JwtBearerOptions> optionsMonitor =
            _factory.Services.GetRequiredService<
                IOptionsMonitor<JwtBearerOptions>>();

        return optionsMonitor.Get(
            JwtBearerDefaults.AuthenticationScheme);
    }

    private static TokenValidatedContext CreateTokenValidatedContext(
        JwtBearerOptions options,
        string? subject)
    {
        var scheme =
            new AuthenticationScheme(
                JwtBearerDefaults.AuthenticationScheme,
                JwtBearerDefaults.AuthenticationScheme,
                typeof(JwtBearerHandler));

        var identity =
            new ClaimsIdentity(
                JwtBearerDefaults.AuthenticationScheme);

        if (subject is not null)
        {
            identity.AddClaim(
                new Claim("sub", subject));
        }

        return new TokenValidatedContext(
            new DefaultHttpContext(),
            scheme,
            options)
        {
            Principal = new ClaimsPrincipal(identity)
        };
    }
}
