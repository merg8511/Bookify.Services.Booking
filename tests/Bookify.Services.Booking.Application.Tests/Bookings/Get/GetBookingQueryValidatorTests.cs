using Bookify.Services.Booking.Application.Bookings.Get;
using Bookify.Services.Booking.Domain.Bookings.ValueObjects;
using Bookify.Services.Booking.Domain.Shared;

namespace Bookify.Services.Booking.Application.Tests.Bookings.Get;

public sealed class GetBookingQueryValidatorTests
{
    private readonly GetBookingQueryValidator
        _validator =
            new();

    [Fact]
    public void Validate_WithValidBookingId_ShouldSucceed()
    {
        var query =
            new GetBookingQuery(
                Guid.NewGuid()
                    .ToString());

        Result result =
            _validator.Validate(
                query);

        Assert.True(
            result.IsSuccess);
    }

    [Fact]
    public void Validate_WithValidBookingReference_ShouldSucceed()
    {
        BookingReference reference =
            BookingReference.New();

        var query =
            new GetBookingQuery(
                reference.Value);

        Result result =
            _validator.Validate(
                query);

        Assert.True(
            result.IsSuccess);
    }

    [Fact]
    public void Validate_WithEmptyGuid_ShouldFail()
    {
        var query =
            new GetBookingQuery(
                Guid.Empty
                    .ToString());

        Result result =
            _validator.Validate(
                query);

        Assert.True(
            result.IsFailure);

        Assert.Equal(
            GetBookingErrors.InvalidIdentifier,
            result.Error);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-a-booking")]
    [InlineData("BK-1234")]
    public void Validate_WithInvalidIdentifier_ShouldFail(
        string identifier)
    {
        var query =
            new GetBookingQuery(
                identifier);

        Result result =
            _validator.Validate(
                query);

        Assert.True(
            result.IsFailure);

        Assert.Equal(
            GetBookingErrors.InvalidIdentifier,
            result.Error);
    }
}
