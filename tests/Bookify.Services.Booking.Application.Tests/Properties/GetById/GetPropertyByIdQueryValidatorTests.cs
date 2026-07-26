using Bookify.Services.Booking.Application.Properties.GetById;
using Bookify.Services.Booking.Domain.Shared;

namespace Bookify.Services.Booking.Application.Tests.Properties.GetById;

public sealed class GetPropertyByIdQueryValidatorTests
{
    [Fact]
    public void Validate_WithValidId_ShouldReturnSuccess()
    {
        // ARRANGE
        var validator =
            new GetPropertyByIdQueryValidator();

        var query =
            new GetPropertyByIdQuery(
                Guid.NewGuid());

        // ACT
        Result result =
            validator.Validate(query);

        // ASSERT
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void Validate_WithEmptyId_ShouldReturnFailure()
    {
        // ARRANGE
        var validator =
            new GetPropertyByIdQueryValidator();

        var query =
            new GetPropertyByIdQuery(
                Guid.Empty);

        // ACT
        Result result =
            validator.Validate(query);

        // ASSERT
        Assert.True(result.IsFailure);

        Assert.Equal(
            GetPropertyByIdErrors.InvalidPropertyId,
            result.Error);
    }

    [Fact]
    public void Validate_WithNullQuery_ShouldThrow()
    {
        // ARRANGE
        var validator =
            new GetPropertyByIdQueryValidator();

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
