using Bookify.Services.Booking.Domain.Bookings.Services;

namespace Bookify.Services.Booking.Domain.Tests.Bookings.Services;

public sealed class WeekendPricingPolicyTests
{
    [Theory]
    [InlineData(10, false)]
    [InlineData(11, true)]
    [InlineData(12, true)]
    [InlineData(13, false)]
    [InlineData(14, false)]
    public void IsWeekendNight_ShouldIdentifyFridayAndSaturday(
        int day,
        bool expectedResult)
    {
        // ARRANGE
        var date =
            new DateOnly(
                2026,
                9,
                day);

        // ACT
        bool result =
            WeekendPricingPolicy.IsWeekendNight(
                date);

        // ASSERT
        Assert.Equal(
            expectedResult,
            result);
    }
}
