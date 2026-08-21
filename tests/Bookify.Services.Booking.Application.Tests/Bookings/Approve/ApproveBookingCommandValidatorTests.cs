using Bookify.Services.Booking.Application.Bookings.Approve;
using Bookify.Services.Booking.Domain.Shared;

namespace Bookify.Services.Booking.Application.Tests.Bookings.Approve;

public sealed class ApproveBookingCommandValidatorTests
{
    [Fact]
    public void Validate_WithValidBookingId_ShouldReturnSuccess()
    {
        // ARRANGE
        var validator =
            new ApproveBookingCommandValidator();

        var command =
            new ApproveBookingCommand(
                Guid.NewGuid());

        // ACT
        Result result =
            validator.Validate(command);

        // ASSERT
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void Validate_WithEmptyBookingId_ShouldReturnFailure()
    {
        // ARRANGE
        var validator =
            new ApproveBookingCommandValidator();

        var command =
            new ApproveBookingCommand(
                Guid.Empty);

        // ACT
        Result result =
            validator.Validate(command);

        // ASSERT
        Assert.True(result.IsFailure);

        Assert.Equal(
            ApproveBookingErrors.InvalidBookingId,
            result.Error);
    }

    [Fact]
    public void Validate_WithNullCommand_ShouldThrow()
    {
        // ARRANGE
        var validator =
            new ApproveBookingCommandValidator();

        // ACT
        void Action()
        {
            validator.Validate(null!);
        }

        // ASSERT
        Assert.Throws<
            ArgumentNullException>(Action);
    }
}
