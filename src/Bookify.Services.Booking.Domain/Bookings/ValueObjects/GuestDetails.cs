using Bookify.Services.Booking.Domain.Bookings.Errors;
using Bookify.Services.Booking.Domain.Shared;
using System.Net.Mail;
using System.Text;

namespace Bookify.Services.Booking.Domain.Bookings.ValueObjects;

public sealed record GuestDetails
{
    public const int MaxFullNameLength = 200;
    public const int MaxEmailLength = 254;
    public const int MinPhoneDigits = 7;
    public const int MaxPhoneDigits = 15;

    private GuestDetails()
    {
        FullName = string.Empty;
        Email = string.Empty;
        Phone = string.Empty;
    }

    private GuestDetails(string fullName, string email, string phone)
    {
        FullName = fullName;
        Email = email;
        Phone = phone;
    }

    public string FullName { get; private set; }
    public string Email { get; private set; }
    public string Phone { get; private set; }

    public static Result<GuestDetails> Create(
        string? fullName,
        string? email,
        string? phone)
    {
        if (string.IsNullOrWhiteSpace(fullName))
        {
            return Result<GuestDetails>.Failure(GuestDetailsErrors.FullNameRequired);
        }

        string normalizedFullName = NormalizeFullName(fullName);

        if (normalizedFullName.Length > MaxFullNameLength)
        {
            return Result<GuestDetails>.Failure(GuestDetailsErrors.FullNameTooLong);
        }

        if (string.IsNullOrWhiteSpace(email))
        {
            return Result<GuestDetails>.Failure(GuestDetailsErrors.EmailRequired);
        }

        string trimmedEmail = email.Trim();

        if (trimmedEmail.Length > MaxEmailLength)
        {
            return Result<GuestDetails>.Failure(GuestDetailsErrors.EmailTooLong);
        }

        if (!TryNormalizeEmail(trimmedEmail, out string normalizedEmail))
        {
            return Result<GuestDetails>.Failure(GuestDetailsErrors.EmailInvalid);
        }

        if (string.IsNullOrWhiteSpace(phone))
        {
            return Result<GuestDetails>.Failure(GuestDetailsErrors.PhoneRequired);
        }

        if (!TryNormalizePhone(phone, out string normalizedPhone))
        {
            return Result<GuestDetails>.Failure(GuestDetailsErrors.PhoneInvalid);
        }

        return Result<GuestDetails>.Success(
            new GuestDetails(
                normalizedFullName,
                normalizedEmail,
                normalizedPhone));
    }

    private static string NormalizeFullName(string fullName)
    {
        string[] parts = fullName
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);

        return string.Join(" ", parts);
    }

    private static bool TryNormalizeEmail(string email, out string normalizedEmail)
    {
        normalizedEmail = string.Empty;

        if (!MailAddress.TryCreate(email, out MailAddress? mailAddress))
        {
            return false;
        }

        if (!string.Equals(
            mailAddress.Address,
            email,
            StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        string address = mailAddress.Address;
        int atIndex = address.LastIndexOf('@');

        if (atIndex <= 0 ||
            atIndex == address.Length - 1)
        {
            return false;
        }

        string localPart = address[..atIndex];
        string domainPart = address[(atIndex + 1)..].ToLowerInvariant();
        normalizedEmail = $"{localPart}@{domainPart}";

        return true;
    }

    private static bool TryNormalizePhone(string phone, out string normalizedPhone)
    {
        normalizedPhone = string.Empty;
        string trimmedPhone = phone.Trim();
        var builder = new StringBuilder(trimmedPhone.Length);

        foreach (char character in trimmedPhone)
        {
            if (character is >= '0' and <= '9')
            {
                builder.Append(character);
                continue;
            }

            if (character == '+' && builder.Length == 0)
            {
                builder.Append(character);
                continue;
            }

            if (char.IsWhiteSpace(character) ||
                character is '-' or '(' or ')' or '.')
            {
                continue;
            }

            return false;
        }

        if (builder.Length == 0)
        {
            return false;
        }

        string candidate = builder.ToString();
        int digitCount = candidate[0] == '+'
            ? candidate.Length - 1
            : candidate.Length;

        if (digitCount < MinPhoneDigits ||
            digitCount > MaxPhoneDigits)
        {
            return false;
        }

        normalizedPhone = candidate;
        return true;
    }
}
