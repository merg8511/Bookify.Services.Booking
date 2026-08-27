using Bookify.Services.Booking.Application.Payments.Initiate;
using Bookify.Services.Booking.Domain.Shared;

namespace Bookify.Services.Booking.Application.Tests.Payments.Initiate;

public sealed class InitiatePaymentCommandValidatorTests
{
    private readonly InitiatePaymentCommandValidator
        _validator = new();

    [Fact]
    public void Validate_WithValidCommand_ReturnsSuccess()
    {
        // Arrange
        var command =
            new InitiatePaymentCommand(
                Guid.NewGuid(),
                "payment-initiation-001");

        // Act
        Result result =
            _validator.Validate(
                command);

        // Assert
        Assert.True(
            result.IsSuccess);
    }

    [Fact]
    public void Validate_WithEmptyBookingId_ReturnsFailure()
    {
        // Arrange
        var command =
            new InitiatePaymentCommand(
                Guid.Empty,
                "payment-initiation-001");

        // Act
        Result result =
            _validator.Validate(
                command);

        // Assert
        Assert.True(
            result.IsFailure);

        Assert.Equal(
            InitiatePaymentErrors.InvalidBookingId,
            result.Error);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_WithInvalidIdempotencyKey_ReturnsFailure(
        string idempotencyKey)
    {
        // Arrange
        var command =
            new InitiatePaymentCommand(
                Guid.NewGuid(),
                idempotencyKey);

        // Act
        Result result =
            _validator.Validate(
                command);

        // Assert
        Assert.True(
            result.IsFailure);

        Assert.Equal(
            InitiatePaymentErrors
                .IdempotencyKeyRequired,
            result.Error);
    }

    [Fact]
    public void Validate_WithMultipleErrors_ReturnsFirstError()
    {
        // Arrange
        var command =
            new InitiatePaymentCommand(
                Guid.Empty,
                "   ");

        // Act
        Result result =
            _validator.Validate(
                command);

        // Assert
        Assert.Equal(
            InitiatePaymentErrors.InvalidBookingId,
            result.Error);
    }

    [Fact]
    public void Validate_WithNullCommand_ThrowsArgumentNullException()
    {
        // Act
        void Action()
        {
            _validator.Validate(
                null!);
        }

        // Assert
        Assert.Throws<
            ArgumentNullException>(
                Action);
    }
}
