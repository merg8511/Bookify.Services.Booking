using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Bookify.Services.Booking.Integration.Tests.Infrastructure;

internal static class TestIdentityTokens
{
    private const string Secret = "Bookify-IntegrationTests-Only-Signing-Key-2026-Do-Not-Use-In-Production";

    internal static SymmetricSecurityKey SigningKey { get; } = new(Encoding.UTF8.GetBytes(Secret));

    internal static string Create(string subject, params string[] roles)
    {
        var claims = new List<Claim>
        {
            new("sub", subject)
        };

        foreach (string role in roles)
        {
            claims.Add(new Claim("roles", role));
        }

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = BookingApiFactory.IdentityAuthority,
            Audience = BookingApiFactory.IdentityAudience,
            Subject = new ClaimsIdentity(claims),
            NotBefore = DateTime.UtcNow.AddMinutes(-1),
            Expires = DateTime.UtcNow.AddMinutes(15),
            SigningCredentials = new SigningCredentials(SigningKey, SecurityAlgorithms.HmacSha256)
        };

        return new JsonWebTokenHandler().CreateToken(descriptor);
    }
}
