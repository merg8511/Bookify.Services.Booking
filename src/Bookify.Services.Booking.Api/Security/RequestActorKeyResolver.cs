using Bookify.Services.Booking.Application.Abstractions.Security;
using System.Runtime.Intrinsics.Arm;
using System.Security.Cryptography;
using System.Text;

namespace Bookify.Services.Booking.Api.Security;

internal static class RequestActorKeyResolver
{
    private const string UnknownAddress = "unknown";

    public static string ResolveIdentityOrIp(HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        string? subject = GetAuthenticatedSubject(httpContext);

        return subject is not null ? $"sub:{subject}" : ResolveIp(httpContext);
    }

    public static string ResolveBookingActorOrIp(HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        string? subject = GetAuthenticatedSubject(httpContext);

        if (subject is not null)
        {
            return $"sub:{subject}";
        }

        string? guestAccessToken = BookingGuestAccessHeader.GetToken(httpContext.Request);

        if (guestAccessToken is not null)
        {
            return $"guest:{CreateFingerPrint(guestAccessToken)}";
        }

        return ResolveIp(httpContext);
    }

    public static string ResolveIp(HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        string address = httpContext.Connection.RemoteIpAddress?.ToString() ?? UnknownAddress;

        return $"ip:{address}";
    }

    public static string CreateFingerPrint(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        byte[] input = Encoding.UTF8.GetBytes(value);

        try
        {
            byte[] hash = SHA256.HashData(input);

            return Convert.ToHexString(hash);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(input);
        }
    }

    private static string? GetAuthenticatedSubject(HttpContext httpContext)
    {
        if (httpContext.User.Identity?.IsAuthenticated != true)
        {
            return null;
        }

        string? subject = httpContext.User.FindFirst(BookifyClaimTypes.Subject)?.Value;

        return string.IsNullOrWhiteSpace(subject)
            ? null
            : subject;
    }
}
