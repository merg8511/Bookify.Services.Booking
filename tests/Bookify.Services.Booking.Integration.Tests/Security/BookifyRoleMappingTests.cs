
using Bookify.Services.Booking.Application.Abstractions.Security;
using Bookify.Services.Booking.Integration.Tests.Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System.Security.Claims;

namespace Bookify.Services.Booking.Integration.Tests.Security;

[Collection(BookingApiTestFixture.Name)]
[Trait("Category", "Integration")]
public sealed class BookifyRoleMappingTests
{
    private readonly BookingApiFactory _factory;

    public BookifyRoleMappingTests(
        BookingApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public void JwtBearer_ShouldUseBookifyRoleClaimType()
    {
        // Arrange
        JwtBearerOptions options =
            GetJwtBearerOptions();

        // Assert
        Assert.Equal(
            "roles",
            BookifyRoles.ClaimType);

        Assert.Equal(
            BookifyRoles.ClaimType,
            options.TokenValidationParameters.RoleClaimType);

        Assert.False(options.MapInboundClaims);
    }

    [Theory]
    [InlineData(BookifyRoles.Admin)]
    [InlineData(BookifyRoles.Owner)]
    [InlineData(BookifyRoles.Customer)]
    public void Principal_WithSupportedRole_ShouldRecognizeAssignedRole(
        string assignedRole)
    {
        // Arrange
        ClaimsPrincipal principal =
            CreatePrincipal(
                new Claim(
                    BookifyRoles.ClaimType,
                    assignedRole));

        // Assert
        Assert.True(
            principal.Identity?.IsAuthenticated == true);

        Assert.True(
            principal.IsInRole(assignedRole));

        string[] supportedRoles =
        [
            BookifyRoles.Admin,
            BookifyRoles.Owner,
            BookifyRoles.Customer
        ];

        foreach (string role in supportedRoles)
        {
            if (role == assignedRole)
            {
                continue;
            }

            Assert.False(
                principal.IsInRole(role));
        }
    }

    [Fact]
    public void Principal_WithMultipleRoles_ShouldRecognizeEachAssignedRole()
    {
        // Arrange
        ClaimsPrincipal principal =
            CreatePrincipal(
                new Claim(
                    BookifyRoles.ClaimType,
                    BookifyRoles.Owner),

                new Claim(
                    BookifyRoles.ClaimType,
                    BookifyRoles.Customer));

        // Assert
        Assert.True(
            principal.IsInRole(BookifyRoles.Owner));

        Assert.True(
            principal.IsInRole(BookifyRoles.Customer));

        Assert.False(
            principal.IsInRole(BookifyRoles.Admin));
    }

    [Fact]
    public void Principal_WithoutRoles_ShouldNotInferCustomerRole()
    {
        // Arrange
        ClaimsPrincipal principal =
            CreatePrincipal();

        // Assert
        Assert.True(
            principal.Identity?.IsAuthenticated == true);

        Assert.NotNull(
            principal.FindFirst("sub"));

        Assert.False(
            principal.IsInRole(BookifyRoles.Admin));

        Assert.False(
            principal.IsInRole(BookifyRoles.Owner));

        Assert.False(
            principal.IsInRole(BookifyRoles.Customer));
    }

    [Fact]
    public void Principal_WithUnknownRole_ShouldNotGrantBookifyRoles()
    {
        // Arrange
        ClaimsPrincipal principal =
            CreatePrincipal(
                new Claim(
                    BookifyRoles.ClaimType,
                    "Support"));

        // Assert
        Assert.False(
            principal.IsInRole(BookifyRoles.Admin));

        Assert.False(
            principal.IsInRole(BookifyRoles.Owner));

        Assert.False(
            principal.IsInRole(BookifyRoles.Customer));
    }

    [Theory]
    [InlineData("admin", BookifyRoles.Admin)]
    [InlineData("owner", BookifyRoles.Owner)]
    [InlineData("customer", BookifyRoles.Customer)]
    public void Principal_WithDifferentRoleCasing_ShouldNotMatch(
        string providedRole,
        string expectedRole)
    {
        // Arrange
        ClaimsPrincipal principal =
            CreatePrincipal(
                new Claim(
                    BookifyRoles.ClaimType,
                    providedRole));

        // Assert
        Assert.False(
            principal.IsInRole(expectedRole));
    }

    [Fact]
    public void Principal_WithDifferentClaimType_ShouldNotGrantBookifyRole()
    {
        // Arrange
        ClaimsPrincipal principal =
            CreatePrincipal(
                new Claim(
                    ClaimTypes.Role,
                    BookifyRoles.Admin),

                new Claim(
                    "realm_access.roles",
                    BookifyRoles.Owner),

                new Claim(
                    "role",
                    BookifyRoles.Customer));

        // Assert
        Assert.False(
            principal.IsInRole(BookifyRoles.Admin));

        Assert.False(
            principal.IsInRole(BookifyRoles.Owner));

        Assert.False(
            principal.IsInRole(BookifyRoles.Customer));
    }

    private ClaimsPrincipal CreatePrincipal(
        params Claim[] roleClaims)
    {
        JwtBearerOptions options =
            GetJwtBearerOptions();

        var claims = new List<Claim>
        {
            new("sub", "customer-123")
        };

        claims.AddRange(roleClaims);

        var identity =
            new ClaimsIdentity(
                claims,
                JwtBearerDefaults.AuthenticationScheme,
                options.TokenValidationParameters.NameClaimType,
                options.TokenValidationParameters.RoleClaimType);

        return new ClaimsPrincipal(identity);
    }

    private JwtBearerOptions GetJwtBearerOptions()
    {
        IOptionsMonitor<JwtBearerOptions> optionsMonitor =
            _factory.Services.GetRequiredService<
                IOptionsMonitor<JwtBearerOptions>>();

        return optionsMonitor.Get(
            JwtBearerDefaults.AuthenticationScheme);
    }
}
