using Bookify.Services.Booking.Application.Availability.Get;
using Bookify.Services.Booking.Domain.Bookings.Errors;
using Bookify.Services.Booking.Domain.Bookings.ValueObjects;
using Bookify.Services.Booking.Domain.Shared;

namespace Bookify.Services.Booking.Application.Tests.Availability.Get;

public sealed class GetAvailabilityQueryValidatorTests
{
    private readonly GetAvailabilityQueryValidator _validator = new();

    [Fact]
    public void Validate_WithValidQuery_ReturnsSuccess()
    {
        var query = CreateValidQuery();

        Result result = _validator.Validate(query);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void Validate_WithEmptyProperty_ReturnsFailure()
    {
        var query = CreateValidQuery() with { PropertyId = Guid.Empty };

        Result result = _validator.Validate(query);

        Assert.Equal(
            GetAvailabilityErrors
                .InvalidPropertyId,
            result.Error);
    }

    [Fact]
    public void Validate_WithoutCheckInDate_ReturnsFailure()
    {
        var query = CreateValidQuery() with { CheckInDate = null };

        Result result = _validator.Validate(query);

        Assert.Equal(
            GetAvailabilityErrors
                .CheckInDateRequired,
            result.Error);
    }

    [Fact]
    public void Validate_WithoutCheckOutDate_ReturnsFailure()
    {
        var query = CreateValidQuery() with
        {
            CheckOutDate = null
        };

        Result result = _validator.Validate(query);

        Assert.Equal(
            GetAvailabilityErrors
                .CheckOutDateRequired,
            result.Error);
    }

    [Fact]
    public void Validate_WithoutGuestCount_ReturnsFailure()
    {
        var query = CreateValidQuery() with
        {
            GuestCount = null
        };

        Result result = _validator.Validate(query);

        Assert.Equal(
            GetAvailabilityErrors
                .GuestCountRequired,
            result.Error);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-20)]
    public void Validate_WithInvalidGuestCount_ReturnsFailure(int guestCount)
    {
        var query = CreateValidQuery() with
        {
            GuestCount = guestCount
        };

        Result result = _validator.Validate(query);

        Assert.Equal(
            GetAvailabilityErrors
                .InvalidGuestCount,
            result.Error);
    }

    [Fact]
    public void Validate_WithInvalidDateRange_ReturnsFailure()
    {
        DateOnly date = new(
            2026,
            8,
            10);

        var query = CreateValidQuery() with
        {
            CheckInDate = date,
            CheckOutDate = date
        };

        Result result = _validator.Validate(query);

        Assert.Equal(
            StayPeriodErrors
                .InvalidDateRange,
            result.Error);
    }

    private static GetAvailabilityQuery CreateValidQuery()
    {
        return new GetAvailabilityQuery(
            Guid.NewGuid(),
            new DateOnly(
                2026,
                8, 10),
            new DateOnly(
                2026,
                8,
                15),
            GuestCount: 2);
    }
}
