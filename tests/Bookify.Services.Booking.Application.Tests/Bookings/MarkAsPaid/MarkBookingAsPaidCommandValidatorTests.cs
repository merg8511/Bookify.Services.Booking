using Bookify.Services.Booking.Application.Bookings.MarkAsPaid;
using Bookify.Services.Booking.Domain.Shared;

namespace Bookify.Services.Booking.Application.Tests.Bookings.MarkAsPaid;

public sealed class MarkBookingAsPaidCommandValidatorTests
{
    [Fact]
    public void Validate_WithValidBookingId_ShouldReturnSuccess()
    {
        var validator =
            new MarkBookingAsPaidCommandValidator();

        var command =
            new MarkBookingAsPaidCommand(
                Guid.NewGuid());

        Result result =
            validator.Validate(command);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void Validate_WithEmptyBookingId_ShouldReturnFailure()
    {
        var validator =
            new MarkBookingAsPaidCommandValidator();

        var command =
            new MarkBookingAsPaidCommand(
                Guid.Empty);

        Result result =
            validator.Validate(command);

        Assert.True(result.IsFailure);

        Assert.Equal(
            MarkBookingAsPaidErrors.InvalidBookingId,
            result.Error);
    }

    [Fact]
    public void Validate_WithNullCommand_ShouldThrow()
    {
        var validator =
            new MarkBookingAsPaidCommandValidator();

        void Action()
        {
            validator.Validate(null!);
        }

        Assert.Throws<
            ArgumentNullException>(Action);
    }
}
