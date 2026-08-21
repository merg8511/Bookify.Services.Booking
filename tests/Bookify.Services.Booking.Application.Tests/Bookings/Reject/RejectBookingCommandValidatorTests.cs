using Bookify.Services.Booking.Application.Bookings.Reject;
using Bookify.Services.Booking.Domain.Shared;

namespace Bookify.Services.Booking.Application.Tests.Bookings.Reject;

public sealed class RejectBookingCommandValidatorTests
{
    [Fact]
    public void Validate_WithValidBookingId_ShouldReturnSuccess()
    {
        // ARRANGE
        var validator = new RejectBookingCommandValidator();

        var command = new RejectBookingCommand(Guid.NewGuid());

        // ACT
        Result result = validator.Validate(command);

        // ASSERT
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void Validate_WithEmptyBookingId_ShouldReturnFailure()
    {
        // ARRANGE
        var validator = new RejectBookingCommandValidator();

        var command = new RejectBookingCommand(Guid.Empty);

        // ACT
        Result result = validator.Validate(command);

        // ASSERT
        Assert.True(result.IsFailure);

        Assert.Equal(
            RejectBookingErrors.InvalidBookingId,
            result.Error);
    }

    [Fact]
    public void Validate_WithNullCommand_ShouldThrow()
    {
        // ARRANGE
        var validator = new RejectBookingCommandValidator();

        // ACT
        void Action()
        {
            validator.Validate(null!);
        }

        // ASSERT
        Assert.Throws<ArgumentNullException>(Action);
    }
}
