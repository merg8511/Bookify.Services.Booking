using Bookify.Services.Booking.Application.Bookings.Reject;
using Bookify.Services.Booking.Domain.Shared;

namespace Bookify.Services.Booking.Application.Tests.Bookings.Reject;

public sealed class RejectBookingCommandValidatorTests
{
    [Fact]
    public void Validate_WithValidBookingId_ShouldReturnSuccess()
    {
        // Arrange
        var validator = new RejectBookingCommandValidator();
        var command = new RejectBookingCommand(Guid.NewGuid());

        // Act
        Result result = validator.Validate(command);

        // Assert
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void Validate_WithEmptyBookingId_ShouldReturnFailure()
    {
        // Arrange
        var validator = new RejectBookingCommandValidator();
        var command = new RejectBookingCommand(Guid.Empty);

        // Act
        Result result = validator.Validate(command);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(RejectBookingErrors.InvalidBookingId, result.Error);
    }

    [Fact]
    public void Validate_WithNullCommand_ShouldThrow()
    {
        // Arrange
        var validator = new RejectBookingCommandValidator();

        // Act
        void Action() => validator.Validate(null!);

        // Assert
        Assert.Throws<ArgumentNullException>(Action);
    }
}
