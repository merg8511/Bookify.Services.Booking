using Bookify.Services.Booking.Domain.Shared.Errors;
using Bookify.Services.Booking.Domain.Shared.ValueObjects;

namespace Bookify.Services.Booking.Domain.Tests.Shared.ValueObjects;

public sealed class GuestCountTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(20)]
    public void Create_WithPositiveValue_ShouldReturnSuccess(int value)
    {
        // ACT
        var result = GuestCount.Create(value);

        // ASSERT
        Assert.True(result.IsSuccess);
        Assert.Equal(value, result.Value.Value);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(-2)]
    [InlineData(-20)]
    public void Create_WithNonPositiveValue_ShouldReturnFailure(int value)
    {
        // ACT
        var result = GuestCount.Create(value);

        // ASSERT
        Assert.True(result.IsFailure);

        Assert.Equal(
            GuestCountErrors.InvalidValue,
            result.Error);
    }

    [Fact]
    public void GuestCounts_WithSameValue_ShouldBeEqual()
    {
        // ARRANGE
        var firstGuestCount = GuestCount.Create(4).Value;
        var secondGuestCount = GuestCount.Create(4).Value;

        // ASSERT
        Assert.Equal(
            firstGuestCount,
            secondGuestCount);
    }

    [Fact]
    public void GuestCounts_WithDifferentValues_ShouldNotBeEqual()
    {
        // ARRANGE
        var firstGuestCount = GuestCount.Create(2).Value;
        var secondGuestCount = GuestCount.Create(4).Value;

        // ASSERT
        Assert.NotEqual(
            firstGuestCount,
            secondGuestCount);
    }
}
