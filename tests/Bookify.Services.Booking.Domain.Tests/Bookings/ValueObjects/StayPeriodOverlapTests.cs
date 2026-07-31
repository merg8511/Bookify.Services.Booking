using Bookify.Services.Booking.Domain.Bookings.ValueObjects;

namespace Bookify.Services.Booking.Domain.Tests.Bookings.ValueObjects;

public sealed class StayPeriodOverlapTests
{
    public static TheoryData<
        DateOnly,
        DateOnly,
        DateOnly,
        DateOnly,
        bool> OverlapCases =>
        new()
        {
            {
                Date(10),
                Date(15),
                Date(10),
                Date(15),
                true
            },
            {
                Date(10),
                Date(15),
                Date(12),
                Date(18),
                true
            },
            {
                Date(12),
                Date(18),
                Date(10),
                Date(15),
                true
            },
            {
                Date(10),
                Date(20),
                Date(12),
                Date(15),
                true
            },
            {
                Date(12),
                Date(15),
                Date(10),
                Date(20),
                true
            },
            {
                Date(10),
                Date(12),
                Date(12),
                Date(15),
                false
            },
            {
                Date(12),
                Date(15),
                Date(10),
                Date(12),
                false
            },
            {
                Date(10),
                Date(12),
                Date(15),
                Date(18),
                false
            },
            {
                Date(15),
                Date(18),
                Date(10),
                Date(12),
                false
            }
        };

    [Theory]
    [MemberData(nameof(OverlapCases))]
    public void Overlaps_WithValidPeriods_ReturnsExpectedResult(
        DateOnly firstCheckInDate,
        DateOnly firstCheckOutDate,
        DateOnly secondCheckInDate,
        DateOnly secondCheckOutDate,
        bool expectedResult)
    {
        // ARRANGE
        StayPeriod firstPeriod =
            CreatePeriod(
                firstCheckInDate,
                firstCheckOutDate);

        StayPeriod secondPeriod =
            CreatePeriod(
                secondCheckInDate,
                secondCheckOutDate);

        // ACT
        bool overlaps = firstPeriod.Overlaps(secondPeriod);

        // ASSERT
        Assert.Equal(
            expectedResult,
            overlaps);
    }

    [Fact]
    public void Overlaps_WithNullPeriod_ThrowsArgumentNullExeption()
    {
        // ARRANGE
        StayPeriod period =
            CreatePeriod(
                Date(10),
                Date(15));

        // ACT
        void Action()
        {
            period.Overlaps(
                null!);
        }

        // ASSERT
        Assert.Throws<
            ArgumentNullException>(
                Action);
    }

    private static StayPeriod CreatePeriod(
        DateOnly checkInDate,
        DateOnly checkOutDate)
    {
        return StayPeriod.Create(
            checkInDate,
            checkOutDate)
            .Value;
    }

    private static DateOnly Date(int day)
    {
        return new DateOnly(
            2026,
            8,
            day);
    }
}


