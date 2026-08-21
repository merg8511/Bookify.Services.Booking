using Bookify.Services.Booking.Application.Bookings.Complete;
using Bookify.Services.Booking.Domain.Shared;

namespace Bookify.Services.Booking.Application.Tests.Bookings.Complete;

public sealed class CompleteBookingCommandValidatorTests
{
    [Fact]
    public void Validate_WithValidBookingId_ShouldReturnSuccess()
    {
        var validator =
            new CompleteBookingCommandValidator();

        var command =
            new CompleteBookingCommand(
                Guid.NewGuid());

        Result result =
            validator.Validate(command);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void Validate_WithEmptyBookingId_ShouldReturnFailure()
    {
        var validator =
            new CompleteBookingCommandValidator();

        var command =
            new CompleteBookingCommand(
                Guid.Empty);

        Result result =
            validator.Validate(command);

        Assert.True(result.IsFailure);

        Assert.Equal(
            CompleteBookingErrors.InvalidBookingId,
            result.Error);
    }

    [Fact]
    public void Validate_WithNullCommand_ShouldThrow()
    {
        var validator =
            new CompleteBookingCommandValidator();

        void Action()
        {
            validator.Validate(null!);
        }

        Assert.Throws<
            ArgumentNullException>(Action);
    }
}
