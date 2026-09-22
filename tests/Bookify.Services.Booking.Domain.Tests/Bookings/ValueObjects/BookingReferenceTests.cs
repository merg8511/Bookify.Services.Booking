using Bookify.Services.Booking.Domain.Bookings.Errors;
using Bookify.Services.Booking.Domain.Bookings.ValueObjects;
using Bookify.Services.Booking.Domain.Shared;

namespace Bookify.Services.Booking.Domain.Tests.Bookings.ValueObjects;

public sealed class BookingReferenceTests
{
    [Fact]
    public void New_ShouldCreateReferenceWithExpectedFormat()
    {
        // ACT
        BookingReference reference =
            BookingReference.New();

        // ASSERT
        Assert.NotNull(reference);

        Assert.Equal(
            BookingReference.MaxLength,
            reference.Value.Length);

        Assert.Matches(
            "^BK-[0-9A-F]{4}-[0-9A-F]{4}-[0-9A-F]{4}-[0-9A-F]{4}-[0-9A-F]{4}$",
            reference.Value);
    }

    [Fact]
    public void Create_WithValidLowercaseReference_ShouldNormalizeToUppercase()
    {
        // ACT
        Result<BookingReference> result =
            BookingReference.Create(
                "  bk-abcd-1234-ef56-7890-abcd  ");

        // ASSERT
        Assert.True(result.IsSuccess);

        Assert.Equal(
            "BK-ABCD-1234-EF56-7890-ABCD",
            result.Value.Value);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WhenReferenceIsMissing_ShouldFail(
        string? value)
    {
        // ACT
        Result<BookingReference> result =
            BookingReference.Create(value);

        // ASSERT
        Assert.True(result.IsFailure);

        Assert.Equal(
            BookingReferenceErrors.Required,
            result.Error);
    }

    [Theory]
    [InlineData("BK-1234")]
    [InlineData("XX-1234-1234-1234-1234-1234")]
    [InlineData("BK-123-1234-1234-1234-1234")]
    [InlineData("BK-12345-1234-1234-1234-1234")]
    [InlineData("BK-1234-1234-1234-1234-ZZZZ")]
    [InlineData("BK_1234_1234_1234_1234_1234")]
    public void Create_WithInvalidFormat_ShouldFail(
        string value)
    {
        // ACT
        Result<BookingReference> result =
            BookingReference.Create(value);

        // ASSERT
        Assert.True(result.IsFailure);

        Assert.Equal(
            BookingReferenceErrors.InvalidFormat,
            result.Error);
    }
}
