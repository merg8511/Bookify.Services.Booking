using Bookify.Services.Booking.Application.Bookings.ExpirePayment;
using Bookify.Services.Booking.Domain.Shared;

namespace Bookify.Services.Booking.Application.Tests.Bookings.ExpirePayment;

public sealed class ExpireBookingPaymentCommandValidatorTests
{
    [Fact]
    public void Validate_WithValidBookingId_ShouldReturnSuccess()
    {
        var validator =
            new ExpireBookingPaymentCommandValidator();

        var command =
            new ExpireBookingPaymentCommand(
                Guid.NewGuid());

        Result result =
            validator.Validate(command);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void Validate_WithEmptyBookingId_ShouldReturnFailure()
    {
        var validator =
            new ExpireBookingPaymentCommandValidator();

        var command =
            new ExpireBookingPaymentCommand(
                Guid.Empty);

        Result result =
            validator.Validate(command);

        Assert.True(result.IsFailure);

        Assert.Equal(
            ExpireBookingPaymentErrors.InvalidBookingId,
            result.Error);
    }

    [Fact]
    public void Validate_WithNullCommand_ShouldThrow()
    {
        var validator =
            new ExpireBookingPaymentCommandValidator();

        void Action()
        {
            validator.Validate(null!);
        }

        Assert.Throws<
            ArgumentNullException>(Action);
    }
}
