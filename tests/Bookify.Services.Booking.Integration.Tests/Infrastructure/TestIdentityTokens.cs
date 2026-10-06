using System.Security.Claims;
using System.Text;
using Bookify.Services.Booking.Application.Abstractions.Security;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Bookify.Services.Booking.Integration.Tests.Infrastructure;

internal static class TestIdentityTokens
{
    private const string Secret = "Bookify-IntegrationTests-Only-Signing-Key-2026-Do-Not-Use-In-Production";

    internal static SymmetricSecurityKey SigningKey { get; } = new(Encoding.UTF8.GetBytes(Secret));

    internal static string Create(string subject, params string[] roles)
    {
        ClaimsIdentity identity = CreateIdentity(subject, JwtBearerDefaults.AuthenticationScheme, roles);

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = BookingApiFactory.IdentityAuthority,
            Audience = BookingApiFactory.IdentityAudience,
            Subject = identity,
            NotBefore = DateTime.UtcNow.AddMinutes(-1),
            Expires = DateTime.UtcNow.AddMinutes(15),
            SigningCredentials = new SigningCredentials(SigningKey, SecurityAlgorithms.HmacSha256)
        };

        return new JsonWebTokenHandler().CreateToken(descriptor);
    }

    internal static ClaimsPrincipal CreatePrincipal(string subject, params string[] roles)
    {
        return new ClaimsPrincipal(CreateIdentity(subject, JwtBearerDefaults.AuthenticationScheme, roles));
    }

    internal static ClaimsPrincipal CreateAnonymousPrincipal(string? subject = null, params string[] roles)
    {
        return new ClaimsPrincipal(CreateIdentity(subject, authenticationType: null, roles));
    }

    private static ClaimsIdentity CreateIdentity(string? subject, string? authenticationType, params string[] roles)
    {
        var claims = new List<Claim>();

        if (subject is not null)
        {
            claims.Add(new Claim(BookifyClaimTypes.Subject, subject));
        }

        foreach (string role in roles)
        {
            claims.Add(new Claim(BookifyRoles.ClaimType, role));
        }

        return new ClaimsIdentity(claims, authenticationType, BookifyClaimTypes.Subject, BookifyRoles.ClaimType);
    }
}
