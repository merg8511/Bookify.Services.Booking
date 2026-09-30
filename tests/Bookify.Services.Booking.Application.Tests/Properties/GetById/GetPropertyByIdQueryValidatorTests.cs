using Bookify.Services.Booking.Application.Properties.GetById;
using Bookify.Services.Booking.Domain.Shared;

namespace Bookify.Services.Booking.Application.Tests.Properties.GetById;

public sealed class GetPropertyByIdQueryValidatorTests
{
    [Fact]
    public void Validate_WithValidId_ShouldReturnSuccess()
    {
        // Arrange
        var validator = new GetPropertyByIdQueryValidator();
        var query = new GetPropertyByIdQuery(Guid.NewGuid());

        // Act
        Result result = validator.Validate(query);

        // Assert
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void Validate_WithEmptyId_ShouldReturnFailure()
    {
        // Arrange
        var validator = new GetPropertyByIdQueryValidator();
        var query = new GetPropertyByIdQuery(Guid.Empty);

        // Act
        Result result = validator.Validate(query);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(GetPropertyByIdErrors.InvalidPropertyId, result.Error);
    }

    [Fact]
    public void Validate_WithNullQuery_ShouldThrow()
    {
        // Arrange
        var validator = new GetPropertyByIdQueryValidator();

        // Act
        void Action() => validator.Validate(null!);

        // Assert
        Assert.Throws<ArgumentNullException>(Action);
    }
}
