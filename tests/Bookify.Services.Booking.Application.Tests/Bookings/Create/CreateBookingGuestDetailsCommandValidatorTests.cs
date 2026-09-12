using Bookify.Services.Booking.Application.Bookings.Create;
using Bookify.Services.Booking.Domain.Bookings.Errors;
using Bookify.Services.Booking.Domain.Bookings.ValueObjects;
using Bookify.Services.Booking.Domain.Shared;

namespace Bookify.Services.Booking.Application.Tests.Bookings.Create;

public sealed class
    CreateBookingGuestDetailsCommandValidatorTests
{
    private readonly
        CreateBookingCommandValidator _validator =
            new();

    [Fact]
    public void Validate_WithValidGuestDetails_ReturnsSuccess()
    {
        // ARRANGE
        CreateBookingCommand command =
            CreateValidCommand();

        // ACT
        Result result =
            _validator.Validate(
                command);

        // ASSERT
        Assert.True(
            result.IsSuccess);
    }

    [Fact]
    public void Validate_WithoutGuestFullName_ReturnsFailure()
    {
        // ARRANGE
        CreateBookingCommand command =
            CreateValidCommand() with
            {
                GuestFullName = "   "
            };

        // ACT
        Result result =
            _validator.Validate(
                command);

        // ASSERT
        Assert.True(
            result.IsFailure);

        Assert.Equal(
            GuestDetailsErrors.FullNameRequired,
            result.Error);
    }

    [Fact]
    public void Validate_WithGuestFullNameTooLong_ReturnsFailure()
    {
        // ARRANGE
        CreateBookingCommand command =
            CreateValidCommand() with
            {
                GuestFullName =
                    new string(
                        'A',
                        GuestDetails
                            .MaxFullNameLength + 1)
            };

        // ACT
        Result result =
            _validator.Validate(
                command);

        // ASSERT
        Assert.True(
            result.IsFailure);

        Assert.Equal(
            GuestDetailsErrors.FullNameTooLong,
            result.Error);
    }

    [Fact]
    public void Validate_WithoutGuestEmail_ReturnsFailure()
    {
        // ARRANGE
        CreateBookingCommand command =
            CreateValidCommand() with
            {
                GuestEmail = null
            };

        // ACT
        Result result =
            _validator.Validate(
                command);

        // ASSERT
        Assert.True(
            result.IsFailure);

        Assert.Equal(
            GuestDetailsErrors.EmailRequired,
            result.Error);
    }

    [Fact]
    public void Validate_WithInvalidGuestEmail_ReturnsFailure()
    {
        // ARRANGE
        CreateBookingCommand command =
            CreateValidCommand() with
            {
                GuestEmail =
                    "not-an-email"
            };

        // ACT
        Result result =
            _validator.Validate(
                command);

        // ASSERT
        Assert.True(
            result.IsFailure);

        Assert.Equal(
            GuestDetailsErrors.EmailInvalid,
            result.Error);
    }

    [Fact]
    public void Validate_WithoutGuestPhone_ReturnsFailure()
    {
        // ARRANGE
        CreateBookingCommand command =
            CreateValidCommand() with
            {
                GuestPhone = null
            };

        // ACT
        Result result =
            _validator.Validate(
                command);

        // ASSERT
        Assert.True(
            result.IsFailure);

        Assert.Equal(
            GuestDetailsErrors.PhoneRequired,
            result.Error);
    }

    [Fact]
    public void Validate_WithInvalidGuestPhone_ReturnsFailure()
    {
        // ARRANGE
        CreateBookingCommand command =
            CreateValidCommand() with
            {
                GuestPhone =
                    "+503/7777/8888"
            };

        // ACT
        Result result =
            _validator.Validate(
                command);

        // ASSERT
        Assert.True(
            result.IsFailure);

        Assert.Equal(
            GuestDetailsErrors.PhoneInvalid,
            result.Error);
    }

    private static CreateBookingCommand
        CreateValidCommand()
    {
        return new CreateBookingCommand(
            Guid.NewGuid(),
            Guid.NewGuid(),
            new DateOnly(
                2026,
                10,
                10),
            new DateOnly(
                2026,
                10,
                15),
            GuestCount: 2,
            GuestFullName:
                "Leonel Alvarenga",
            GuestEmail:
                "leonel@example.com",
            GuestPhone:
                "+50377778888");
    }
}
