using Bookify.Services.Booking.Application.Bookings.Create;
using Bookify.Services.Booking.Application.Tests.Infrastructure;
using Bookify.Services.Booking.Domain.Bookings.Errors;
using Bookify.Services.Booking.Domain.Bookings.ValueObjects;
using Bookify.Services.Booking.Domain.Shared;

namespace Bookify.Services.Booking.Application.Tests.Bookings.Create;

public sealed class CreateBookingGuestDetailsCommandValidatorTests
{
    private readonly CreateBookingCommandValidator _validator = new();

    [Fact]
    public void Validate_WithValidGuestDetails_ReturnsSuccess()
    {
        CreateBookingCommand command = BookingCommandTestFactory.CreateBookingCommand(
            guestFullName: "Leonel Alvarenga",
            guestEmail: "leonel@example.com",
            guestPhone: "+50377778888");

        Result result = _validator.Validate(command);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void Validate_WithoutGuestFullName_ReturnsFailure()
    {
        CreateBookingCommand command = BookingCommandTestFactory.CreateBookingCommand(guestFullName: "   ");

        Result result = _validator.Validate(command);

        Assert.True(result.IsFailure);
        Assert.Equal(GuestDetailsErrors.FullNameRequired, result.Error);
    }

    [Fact]
    public void Validate_WithGuestFullNameTooLong_ReturnsFailure()
    {
        CreateBookingCommand command = BookingCommandTestFactory.CreateBookingCommand(
            guestFullName: new string('A', GuestDetails.MaxFullNameLength + 1));

        Result result = _validator.Validate(command);

        Assert.True(result.IsFailure);
        Assert.Equal(GuestDetailsErrors.FullNameTooLong, result.Error);
    }

    [Fact]
    public void Validate_WithoutGuestEmail_ReturnsFailure()
    {
        CreateBookingCommand command = BookingCommandTestFactory.CreateBookingCommand(guestEmail: null);

        Result result = _validator.Validate(command);

        Assert.True(result.IsFailure);
        Assert.Equal(GuestDetailsErrors.EmailRequired, result.Error);
    }

    [Fact]
    public void Validate_WithInvalidGuestEmail_ReturnsFailure()
    {
        CreateBookingCommand command = BookingCommandTestFactory.CreateBookingCommand(guestEmail: "not-an-email");

        Result result = _validator.Validate(command);

        Assert.True(result.IsFailure);
        Assert.Equal(GuestDetailsErrors.EmailInvalid, result.Error);
    }

    [Fact]
    public void Validate_WithoutGuestPhone_ReturnsFailure()
    {
        CreateBookingCommand command = BookingCommandTestFactory.CreateBookingCommand(guestPhone: null);

        Result result = _validator.Validate(command);

        Assert.True(result.IsFailure);
        Assert.Equal(GuestDetailsErrors.PhoneRequired, result.Error);
    }

    [Fact]
    public void Validate_WithInvalidGuestPhone_ReturnsFailure()
    {
        CreateBookingCommand command = BookingCommandTestFactory.CreateBookingCommand(guestPhone: "+503/7777/8888");

        Result result = _validator.Validate(command);

        Assert.True(result.IsFailure);
        Assert.Equal(GuestDetailsErrors.PhoneInvalid, result.Error);
    }
}
