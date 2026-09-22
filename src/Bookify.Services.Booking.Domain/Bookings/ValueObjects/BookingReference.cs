using Bookify.Services.Booking.Domain.Bookings.Errors;
using Bookify.Services.Booking.Domain.Shared;
using System.Security.Cryptography;

namespace Bookify.Services.Booking.Domain.Bookings.ValueObjects;

public sealed record BookingReference
{
    public const string Prefix = "BK";

    public const int SegmentLength = 4;
    public const int SegmentCount = 5;

    public const int TokenLength = SegmentLength * SegmentCount;
    public const int MaxLength = 27;
    private const int RandomByteCount = 10;

    private BookingReference()
    {
        Value = string.Empty;
    }

    private BookingReference(string value)
    {
        Value = value;
    }

    public string Value { get; private set; }

    public static BookingReference New()
    {
        byte[] bytes = RandomNumberGenerator.GetBytes(RandomByteCount);
        string token = Convert.ToHexString(bytes);

        return new BookingReference(FormatToken(token));
    }

    public static Result<BookingReference> Create(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Result<BookingReference>.Failure(BookingReferenceErrors.Required);
        }

        string normalizedValue = value.Trim().ToUpperInvariant();

        if (!IsValid(normalizedValue))
        {
            return Result<BookingReference>.Failure(BookingReferenceErrors.InvalidFormat);
        }

        return Result<BookingReference>.Success(new BookingReference(normalizedValue));
    }

    public override string ToString()
    {
        return Value;
    }
    private static string FormatToken(string token)
    {
        return
            $"{Prefix}-" +
            $"{token[0..4]}-" +
            $"{token[4..8]}-" +
            $"{token[8..12]}-" +
            $"{token[12..16]}-" +
            $"{token[16..20]}";
    }

    private static bool IsValid(string value)
    {
        string[] parts = value.Split('-', StringSplitOptions.None);

        if (parts.Length != SegmentCount + 1)
        {
            return false;
        }

        if (!string.Equals(parts[0], Prefix, StringComparison.Ordinal))
        {
            return false;
        }

        for (int index = 1; index < parts.Length; index++)
        {
            string segment = parts[index];

            if ((segment.Length != SegmentLength))
            {
                return false;
            }

            foreach (char character in segment)
            {
                bool isHexadecimal = character is >= '0' and <= '9'
                    or >= 'A' and <= 'F';

                if (!isHexadecimal)
                {
                    return false;
                }
            }
        }

        return true;
    }
}
