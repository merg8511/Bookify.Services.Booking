using Bookify.Services.Booking.Application.RentableUnits.GetByProperty;
using Bookify.Services.Booking.Domain.Shared;

namespace Bookify.Services.Booking.Application.Tests.RentableUnits.GetByProperty;

public sealed class GetPropertyUnitsQueryValidatorTests
{
    [Fact]
    public void Validate_WithValidPropertyId_ShouldSucceed()
    {
        var validator =
            new GetPropertyUnitsQueryValidator();

        var query =
            new GetPropertyUnitsQuery(
                Guid.NewGuid());

        Result result =
            validator.Validate(
                query);

        Assert.True(
            result.IsSuccess);
    }

    [Fact]
    public void Validate_WithEmptyPropertyId_ShouldFail()
    {
        var validator =
            new GetPropertyUnitsQueryValidator();

        var query =
            new GetPropertyUnitsQuery(
                Guid.Empty);

        Result result =
            validator.Validate(
                query);

        Assert.True(
            result.IsFailure);

        Assert.Equal(
            GetPropertyUnitsErrors
                .InvalidPropertyId,
            result.Error);
    }
}
