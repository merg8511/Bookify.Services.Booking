using Bookify.Services.Booking.Application.Bookings.Approve;
using Bookify.Services.Booking.Domain.Shared;

namespace Bookify.Services.Booking.Application.Tests.Bookings.Approve;

public sealed class ApproveBookingCommandValidatorTests
{
    [Fact]
    public void Validate_WithValidBookingId_ShouldReturnSuccess()
    {
        // Arrange
        var validator = new ApproveBookingCommandValidator();
        var command = new ApproveBookingCommand(Guid.NewGuid());

        // Act
        Result result = validator.Validate(command);

        // Assert
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void Validate_WithEmptyBookingId_ShouldReturnFailure()
    {
        // Arrange
        var validator = new ApproveBookingCommandValidator();
        var command = new ApproveBookingCommand(Guid.Empty);

        // Act
        Result result = validator.Validate(command);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ApproveBookingErrors.InvalidBookingId, result.Error);
    }

    [Fact]
    public void Validate_WithNullCommand_ShouldThrow()
    {
        // Arrange
        var validator = new ApproveBookingCommandValidator();

        // Act
        void Action() => validator.Validate(null!);

        // Assert
        Assert.Throws<ArgumentNullException>(Action);
    }
}
