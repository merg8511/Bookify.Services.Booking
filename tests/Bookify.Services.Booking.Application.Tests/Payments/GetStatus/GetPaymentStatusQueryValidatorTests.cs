using Bookify.Services.Booking.Application.Payments.GetStatus;
using Bookify.Services.Booking.Domain.Shared;

namespace Bookify.Services.Booking.Application.Tests.Payments.GetStatus;

public sealed class GetPaymentStatusQueryValidatorTests
{
    [Fact]
    public void Validate_WhenBookingIdIsValid_ShouldSucceed()
    {
        // Arrange
        var validator =
            new GetPaymentStatusQueryValidator();

        var query =
            new GetPaymentStatusQuery(
                Guid.NewGuid());

        // Act
        Result result =
            validator.Validate(
                query);

        // Assert
        Assert.True(
            result.IsSuccess);
    }

    [Fact]
    public void Validate_WhenBookingIdIsEmpty_ShouldFail()
    {
        // Arrange
        var validator =
            new GetPaymentStatusQueryValidator();

        var query =
            new GetPaymentStatusQuery(
                Guid.Empty);

        // Act
        Result result =
            validator.Validate(
                query);

        // Assert
        Assert.True(
            result.IsFailure);

        Assert.Equal(
            GetPaymentStatusErrors
                .InvalidBookingId,
            result.Error);
    }

    [Fact]
    public void Validate_WithNullQuery_ShouldThrow()
    {
        // Arrange
        var validator =
            new GetPaymentStatusQueryValidator();

        // Act
        void Action()
        {
            validator.Validate(
                null!);
        }

        // Assert
        Assert.Throws<
            ArgumentNullException>(
                Action);
    }
}
