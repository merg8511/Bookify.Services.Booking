using Bookify.Services.Booking.Application.Bookings.Create;
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
        // ARRANGE
        CreateBookingCommand command = CreateValidCommand();

        // ACT
        Result result = _validator.Validate(command);

        // ASSERT
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void Validate_WithEmptyPropertyId_ReturnsFailure()
    {
        // ARRANGE
        CreateBookingCommand command = CreateValidCommand() with
        {
            PropertyId = Guid.Empty
        };

        // ACT
        Result result = _validator.Validate(command);

        // ASSERT
        Assert.True(result.IsFailure);

        Assert.Equal(
            CreateBookingErrors.InvalidPropertyId,
            result.Error);
    }

    [Fact]
    public void Validate_WithEmptyRentableUnitId_ReturnsFailure()
    {
        // ARRANGE
        CreateBookingCommand command = CreateValidCommand() with
        {
            RentableUnitId = Guid.Empty
        };

        // ACT
        Result result = _validator.Validate(command);

        // ASSERT
        Assert.True(result.IsFailure);

        Assert.Equal(
            CreateBookingErrors.InvalidRentableUnitId,
            result.Error);
    }

    [Fact]
    public void Validate_WithoutCheckInDate_ReturnsFailure()
    {
        // ARRANGE
        CreateBookingCommand command = CreateValidCommand() with
        {
            CheckInDate = null
        };

        // ACT
        Result result = _validator.Validate(command);

        // ASSERT
        Assert.True(result.IsFailure);

        Assert.Equal(
            CreateBookingErrors.CheckInDateRequired,
            result.Error);
    }

    [Fact]
    public void Validate_WithoutCheckOutDate_ReturnsFailure()
    {
        // ARRANGE
        CreateBookingCommand command = CreateValidCommand() with
        {
            CheckOutDate = null
        };

        // ACT
        Result result = _validator.Validate(command);

        // ASSERT
        Assert.True(result.IsFailure);

        Assert.Equal(
            CreateBookingErrors.CheckOutDateRequired,
            result.Error);
    }

    [Fact]
    public void Validate_WithoutGuestCount_ReturnsFailure()
    {
        // ARRANGE
        CreateBookingCommand command = CreateValidCommand() with
        {
            GuestCount = null
        };

        // ACT
        Result result = _validator.Validate(command);

        // ASSERT
        Assert.True(result.IsFailure);

        Assert.Equal(
            CreateBookingErrors.GuestCountRequired,
            result.Error);
    }

    [Fact]
    public void Validate_WithSameCheckInAndCheckOut_ReturnsFailure()
    {
        // ARRANGE
        DateOnly date = new(
            2026,
            8,
            10);

        CreateBookingCommand command = CreateValidCommand() with
        {
            CheckInDate = date,
            CheckOutDate = date
        };

        // ACT
        Result result = _validator.Validate(command);

        // ASSERT
        Assert.True(result.IsFailure);

        Assert.Equal(
            StayPeriodErrors.InvalidDateRange,
            result.Error);
    }

    [Fact]
    public void Validate_WithCheckOutBeforeCheckIn_ReturnsFailure()
    {
        // ARRANGE
        CreateBookingCommand command =
            CreateValidCommand() with
            {
                CheckInDate =
                    new DateOnly(
                        2026,
                        8,
                        15),

                CheckOutDate =
                    new DateOnly(
                        2026,
                        8,
                        10)
            };

        // ACT
        Result result = _validator.Validate(command);

        // ASSERT
        Assert.True(result.IsFailure);

        Assert.Equal(
            StayPeriodErrors.InvalidDateRange,
            result.Error);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-20)]
    public void Validate_WithInvalidGuestCount_ReturnsFailure(
        int guestCount)
    {
        // ARRANGE
        CreateBookingCommand command = CreateValidCommand() with
        {
            GuestCount = guestCount
        };

        // ACT
        Result result = _validator.Validate(command);

        // ASSERT
        Assert.True(result.IsFailure);

        Assert.Equal(
            GuestCountErrors.InvalidValue,
            result.Error);
    }

    [Fact]
    public void Validate_WithMultipleErrors_ReturnsFirstError()
    {
        // ARRANGE
        CreateBookingCommand command =
            new(
                Guid.Empty,
                Guid.Empty,
                CheckInDate: null,
                CheckOutDate: null,
                GuestCount: 0);

        // ACT
        Result result =
            _validator.Validate(
                command);

        // ASSERT
        Assert.Equal(
            CreateBookingErrors.InvalidPropertyId,
            result.Error);
    }

    [Fact]
    public void Validate_WithNullCommand_ThrowsArgumentNullException()
    {
        // ACT
        void Action()
        {
            _validator.Validate(
                null!);
        }

        // ASSERT
        Assert.Throws<
            ArgumentNullException>(
                Action);
    }

    private static CreateBookingCommand
        CreateValidCommand()
    {
        return new CreateBookingCommand(
            Guid.NewGuid(),
            Guid.NewGuid(),
            new DateOnly(
                2026,
                8,
                10),
            new DateOnly(
                2026,
                8,
                15),
            GuestCount: 2);
    }

}
