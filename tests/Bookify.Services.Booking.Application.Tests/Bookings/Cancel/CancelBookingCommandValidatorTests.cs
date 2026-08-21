using Bookify.Services.Booking.Application.Bookings.Cancel;
using Bookify.Services.Booking.Domain.Shared;

namespace Bookify.Services.Booking.Application.Tests.Bookings.Cancel;

public sealed class CancelBookingCommandValidatorTests
{
    [Fact]
    public void Validate_WithValidBookingId_ShouldReturnSuccess()
    {
        var validator =
            new CancelBookingCommandValidator();

        var command =
            new CancelBookingCommand(
                Guid.NewGuid());

        Result result =
            validator.Validate(command);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void Validate_WithEmptyBookingId_ShouldReturnFailure()
    {
        var validator =
            new CancelBookingCommandValidator();

        var command =
            new CancelBookingCommand(
                Guid.Empty);

        Result result =
            validator.Validate(command);

        Assert.True(result.IsFailure);

        Assert.Equal(
            CancelBookingErrors.InvalidBookingId,
            result.Error);
    }

    [Fact]
    public void Validate_WithNullCommand_ShouldThrow()
    {
        var validator =
            new CancelBookingCommandValidator();

        void Action()
        {
            validator.Validate(null!);
        }

        Assert.Throws<
            ArgumentNullException>(Action);
    }
}
