using Bookify.Services.Booking.Domain.Bookings.Errors;
using Bookify.Services.Booking.Domain.Bookings.ValueObjects;
using Bookify.Services.Booking.Domain.Shared;

namespace Bookify.Services.Booking.Domain.Tests.Bookings.ValueObjects;

public sealed class GuestDetailsTests
{
    [Fact]
    public void Create_WithValidData_ReturnsSuccessAndNormalizesValues()
    {
        // ARRANGE
        const string fullName =
            "  Leonel\t Enrique   Alvarenga  ";

        const string email =
            "Leonel@EXAMPLE.COM";

        const string phone =
            "+503 (7777) 8888";

        // ACT
        Result<GuestDetails> result =
            GuestDetails.Create(
                fullName,
                email,
                phone);

        // ASSERT
        Assert.True(result.IsSuccess);

        Assert.Equal(
            "Leonel Enrique Alvarenga",
            result.Value.FullName);

        Assert.Equal(
            "Leonel@example.com",
            result.Value.Email);

        Assert.Equal(
            "+50377778888",
            result.Value.Phone);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithoutFullName_ReturnsFailure(
        string? fullName)
    {
        // ACT
        Result<GuestDetails> result =
            GuestDetails.Create(
                fullName,
                "john@example.com",
                "+50377778888");

        // ASSERT
        Assert.True(result.IsFailure);

        Assert.Equal(
            GuestDetailsErrors.FullNameRequired,
            result.Error);
    }

    [Fact]
    public void Create_WithFullNameLongerThanMaximum_ReturnsFailure()
    {
        // ARRANGE
        string fullName =
            new(
                'A',
                GuestDetails.MaxFullNameLength + 1);

        // ACT
        Result<GuestDetails> result =
            GuestDetails.Create(
                fullName,
                "john@example.com",
                "+50377778888");

        // ASSERT
        Assert.True(result.IsFailure);

        Assert.Equal(
            GuestDetailsErrors.FullNameTooLong,
            result.Error);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithoutEmail_ReturnsFailure(
        string? email)
    {
        // ACT
        Result<GuestDetails> result =
            GuestDetails.Create(
                "John Doe",
                email,
                "+50377778888");

        // ASSERT
        Assert.True(result.IsFailure);

        Assert.Equal(
            GuestDetailsErrors.EmailRequired,
            result.Error);
    }

    [Theory]
    [InlineData("not-an-email")]
    [InlineData("john@")]
    [InlineData("@example.com")]
    [InlineData("John Doe <john@example.com>")]
    public void Create_WithInvalidEmail_ReturnsFailure(
        string email)
    {
        // ACT
        Result<GuestDetails> result =
            GuestDetails.Create(
                "John Doe",
                email,
                "+50377778888");

        // ASSERT
        Assert.True(result.IsFailure);

        Assert.Equal(
            GuestDetailsErrors.EmailInvalid,
            result.Error);
    }

    [Fact]
    public void Create_WithEmailLongerThanMaximum_ReturnsFailure()
    {
        // ARRANGE
        string email =
            $"{new string('a', 245)}@example.com";

        Assert.True(
            email.Length >
            GuestDetails.MaxEmailLength);

        // ACT
        Result<GuestDetails> result =
            GuestDetails.Create(
                "John Doe",
                email,
                "+50377778888");

        // ASSERT
        Assert.True(result.IsFailure);

        Assert.Equal(
            GuestDetailsErrors.EmailTooLong,
            result.Error);
    }

    [Fact]
    public void Create_WithUppercaseEmailDomain_NormalizesDomainOnly()
    {
        // ACT
        Result<GuestDetails> result =
            GuestDetails.Create(
                "John Doe",
                "John.Doe@EXAMPLE.COM",
                "+50377778888");

        // ASSERT
        Assert.True(result.IsSuccess);

        Assert.Equal(
            "John.Doe@example.com",
            result.Value.Email);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithoutPhone_ReturnsFailure(
        string? phone)
    {
        // ACT
        Result<GuestDetails> result =
            GuestDetails.Create(
                "John Doe",
                "john@example.com",
                phone);

        // ASSERT
        Assert.True(result.IsFailure);

        Assert.Equal(
            GuestDetailsErrors.PhoneRequired,
            result.Error);
    }

    [Theory]
    [InlineData("+503/7777/8888")]
    [InlineData("+503ABC7777")]
    [InlineData("++50377778888")]
    public void Create_WithUnsupportedPhoneCharacters_ReturnsFailure(
        string phone)
    {
        // ACT
        Result<GuestDetails> result =
            GuestDetails.Create(
                "John Doe",
                "john@example.com",
                phone);

        // ASSERT
        Assert.True(result.IsFailure);

        Assert.Equal(
            GuestDetailsErrors.PhoneInvalid,
            result.Error);
    }

    [Fact]
    public void Create_WithPhoneShorterThanMinimum_ReturnsFailure()
    {
        // ARRANGE
        string phone =
            new(
                '7',
                GuestDetails.MinPhoneDigits - 1);

        // ACT
        Result<GuestDetails> result =
            GuestDetails.Create(
                "John Doe",
                "john@example.com",
                phone);

        // ASSERT
        Assert.True(result.IsFailure);

        Assert.Equal(
            GuestDetailsErrors.PhoneInvalid,
            result.Error);
    }

    [Fact]
    public void Create_WithPhoneLongerThanMaximum_ReturnsFailure()
    {
        // ARRANGE
        string phone =
            new(
                '7',
                GuestDetails.MaxPhoneDigits + 1);

        // ACT
        Result<GuestDetails> result =
            GuestDetails.Create(
                "John Doe",
                "john@example.com",
                phone);

        // ASSERT
        Assert.True(result.IsFailure);

        Assert.Equal(
            GuestDetailsErrors.PhoneInvalid,
            result.Error);
    }

    [Fact]
    public void Create_WithLocalPhoneWithoutPlus_ReturnsSuccess()
    {
        // ACT
        Result<GuestDetails> result =
            GuestDetails.Create(
                "John Doe",
                "john@example.com",
                "7777-8888");

        // ASSERT
        Assert.True(result.IsSuccess);

        Assert.Equal(
            "77778888",
            result.Value.Phone);
    }

    [Fact]
    public void Create_WithPhoneFormatting_RemovesFormattingCharacters()
    {
        // ACT
        Result<GuestDetails> result =
            GuestDetails.Create(
                "John Doe",
                "john@example.com",
                "+503 (7777) 8888");

        // ASSERT
        Assert.True(result.IsSuccess);

        Assert.Equal(
            "+50377778888",
            result.Value.Phone);
    }

    [Fact]
    public void Create_WithMultipleWhitespaceCharactersInName_CollapsesWhitespace()
    {
        // ACT
        Result<GuestDetails> result =
            GuestDetails.Create(
                "  John    Michael\tDoe  ",
                "john@example.com",
                "+50377778888");

        // ASSERT
        Assert.True(result.IsSuccess);

        Assert.Equal(
            "John Michael Doe",
            result.Value.FullName);
    }
}
