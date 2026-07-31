using Bookify.Services.Booking.Domain.Bookings.Errors;
using Bookify.Services.Booking.Domain.Bookings.ValueObjects;

namespace Bookify.Services.Booking.Domain.Tests.Bookings.ValueObjects;

public sealed class StayPeriodTests
{
    [Fact]
    public void Create_WithValidDates_ShouldRerurnSuccess()
    {
        // ARRANGE
        var checkInDate = new DateOnly(2026, 7, 10);
        var checkOutDate = new DateOnly(2026, 7, 12);

        // ACT
        var result = StayPeriod.Create(
            checkInDate,
            checkOutDate);

        // ASSERT
        Assert.True(result.IsSuccess);
        Assert.Equal(checkInDate, result.Value.CheckInDate);
        Assert.Equal(checkOutDate, result.Value.CheckOutDate);
    }

    [Fact]
    public void Create_WithSameCheckInAndCheckOutDate_ShouldReturnFailure()
    {
        // ARRANGE
        var date = new DateOnly(2026, 7, 10);

        // ACT
        var result = StayPeriod.Create(
            date,
            date);

        // ASSERT
        Assert.True(result.IsFailure);
        Assert.Equal(
            StayPeriodErrors.InvalidDateRange,
            result.Error);
    }

    [Fact]
    public void Create_WithCheckOutBeforeCheckIn_ShouldReturnFailure()
    {
        // ARRANGE
        var checkInDate = new DateOnly(2026, 7, 12);
        var checkOutDate = new DateOnly(2026, 7, 10);

        // ACT
        var result = StayPeriod.Create(
            checkInDate,
            checkOutDate);

        // ASSERT
        Assert.True(result.IsFailure);
        Assert.Equal(
            StayPeriodErrors.InvalidDateRange,
            result.Error);
    }

    [Fact]
    public void NumberOfNights_ShouldReturnDifferenceBetweenDates()
    {
        // ARRANGE
        var result = StayPeriod.Create(
            new DateOnly(2026, 7, 10),
            new DateOnly(2026, 7, 13));

        // ACT
        var numberOfNights = result.Value.NumberOfNights;

        // ASSERT
        Assert.Equal(3, numberOfNights);
    }
}
