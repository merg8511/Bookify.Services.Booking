using Bookify.Services.Booking.Application.Bookings.Create;
using Bookify.Services.Booking.Application.Tests.Infrastructure;
using Bookify.Services.Booking.Domain.Bookings.Errors;
using Bookify.Services.Booking.Domain.Shared;
using Bookify.Services.Booking.Domain.Shared.Errors;

namespace Bookify.Services.Booking.Application.Tests.Bookings.Create;

public sealed class CreateBookingCommandValidatorTests
{
    private readonly CreateBookingCommandValidator _validator = new();

    [Fact]
    public void Validate_WithValidCommand_ReturnsSuccess()
    {
        CreateBookingCommand command = BookingCommandTestFactory.CreateBookingCommand();

        Result result = _validator.Validate(command);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void Validate_WithEmptyPropertyId_ReturnsFailure()
    {
        CreateBookingCommand command = BookingCommandTestFactory.CreateBookingCommand(propertyId: Guid.Empty);

        Result result = _validator.Validate(command);

        Assert.True(result.IsFailure);
        Assert.Equal(CreateBookingErrors.InvalidPropertyId, result.Error);
    }

    [Fact]
    public void Validate_WithEmptyRentableUnitId_ReturnsFailure()
    {
        CreateBookingCommand command = BookingCommandTestFactory.CreateBookingCommand(rentableUnitId: Guid.Empty);

        Result result = _validator.Validate(command);

        Assert.True(result.IsFailure);
        Assert.Equal(CreateBookingErrors.InvalidRentableUnitId, result.Error);
    }

    [Fact]
    public void Validate_WithoutCheckInDate_ReturnsFailure()
    {
        CreateBookingCommand command = BookingCommandTestFactory.CreateBookingCommand() with
        {
            CheckInDate = null
        };

        Result result = _validator.Validate(command);

        Assert.True(result.IsFailure);
        Assert.Equal(CreateBookingErrors.CheckInDateRequired, result.Error);
    }

    [Fact]
    public void Validate_WithoutCheckOutDate_ReturnsFailure()
    {
        CreateBookingCommand command = BookingCommandTestFactory.CreateBookingCommand() with
        {
            CheckOutDate = null
        };

        Result result = _validator.Validate(command);

        Assert.True(result.IsFailure);
        Assert.Equal(CreateBookingErrors.CheckOutDateRequired, result.Error);
    }

    [Fact]
    public void Validate_WithoutGuestCount_ReturnsFailure()
    {
        CreateBookingCommand command = BookingCommandTestFactory.CreateBookingCommand() with
        {
            GuestCount = null
        };

        Result result = _validator.Validate(command);

        Assert.True(result.IsFailure);
        Assert.Equal(CreateBookingErrors.GuestCountRequired, result.Error);
    }

    [Fact]
    public void Validate_WithSameCheckInAndCheckOut_ReturnsFailure()
    {
        DateOnly date = new(2026, 8, 10);
        CreateBookingCommand command = BookingCommandTestFactory.CreateBookingCommand(
            checkInDate: date,
            checkOutDate: date);

        Result result = _validator.Validate(command);

        Assert.True(result.IsFailure);
        Assert.Equal(StayPeriodErrors.InvalidDateRange, result.Error);
    }

    [Fact]
    public void Validate_WithCheckOutBeforeCheckIn_ReturnsFailure()
    {
        CreateBookingCommand command = BookingCommandTestFactory.CreateBookingCommand(
            checkInDate: new DateOnly(2026, 8, 15),
            checkOutDate: new DateOnly(2026, 8, 10));

        Result result = _validator.Validate(command);

        Assert.True(result.IsFailure);
        Assert.Equal(StayPeriodErrors.InvalidDateRange, result.Error);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-20)]
    public void Validate_WithInvalidGuestCount_ReturnsFailure(int guestCount)
    {
        CreateBookingCommand command = BookingCommandTestFactory.CreateBookingCommand(guestCount: guestCount);

        Result result = _validator.Validate(command);

        Assert.True(result.IsFailure);
        Assert.Equal(GuestCountErrors.InvalidValue, result.Error);
    }

    [Fact]
    public void Validate_WithMultipleErrors_ReturnsFirstError()
    {
        CreateBookingCommand command = new(
            Guid.Empty,
            Guid.Empty,
            CheckInDate: null,
            CheckOutDate: null,
            GuestCount: 0,
            null,
            null,
            null);

        Result result = _validator.Validate(command);

        Assert.Equal(CreateBookingErrors.InvalidPropertyId, result.Error);
    }

    [Fact]
    public void Validate_WithNullCommand_ThrowsArgumentNullException()
    {
        void Action() => _validator.Validate(null!);

        Assert.Throws<ArgumentNullException>(Action);
    }
}
