using System.Security.Cryptography;
using System.Text;

namespace Bookify.Services.Booking.Domain.Bookings.ValueObjects;

internal sealed class GuestBookingCredential
{
    private const int SecretSizeInBytes = 32;
    private const int TokenLength = 43;
    private const int HashLength = 64;

    private GuestBookingCredential(string rawToken, string hash)
    {
        RawToken = rawToken;
        Hash = hash;
    }

    public string RawToken { get; }
    public string Hash { get; }

    public static GuestBookingCredential Generate()
    {
        byte[] secret = RandomNumberGenerator.GetBytes(SecretSizeInBytes);
        string rawToken;

        try
        {
            rawToken = Convert
                .ToBase64String(secret)
                .TrimEnd('=')
                .Replace('+', '-')
                .Replace('/', '_');
        }
        finally
        {
            CryptographicOperations.ZeroMemory(secret);
        }

        string hash = ComputeHash(rawToken);

        return new GuestBookingCredential(rawToken, hash);
    }

    public static bool Verify(string? rawToken, string? storedHash)
    {
        if (string.IsNullOrWhiteSpace(rawToken) ||
            string.IsNullOrWhiteSpace(storedHash) ||
            rawToken.Length != TokenLength ||
            storedHash.Length != HashLength)
        {
            return false;
        }

        string candidateHash = ComputeHash(rawToken);
        byte[] candidateBytes = Encoding.ASCII.GetBytes(candidateHash);
        byte[] storedBytes = Encoding.ASCII.GetBytes(storedHash);

        try
        {
            return CryptographicOperations.FixedTimeEquals(candidateBytes, storedBytes);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(candidateBytes);
            CryptographicOperations.ZeroMemory(storedBytes);
        }
    }

    private static string ComputeHash(string rawToken)
    {
        byte[] tokenBytes = Encoding.UTF8.GetBytes(rawToken);

        try
        {
            byte[] hash = SHA256.HashData(tokenBytes);

            return Convert.ToHexString(hash);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(tokenBytes);
        }
    }
}
