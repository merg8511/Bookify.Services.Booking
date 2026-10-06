using Bookify.Services.Booking.Domain.Properties;
using Bookify.Services.Booking.Domain.Properties.Errors;
using Bookify.Services.Booking.Domain.Shared;
using Bookify.Services.Booking.Domain.Tests.Infrastructure;

namespace Bookify.Services.Booking.Domain.Tests.Properties;

public sealed class PropertyOwnershipTests
{
    [Fact]
    public void Create_WithValidOwner_ShouldPreserveExactSubject()
    {
        Result<Property> result = CreateProperty("keycloak|owner-123");

        Assert.True(result.IsSuccess);
        Assert.Equal("keycloak|owner-123", result.Value.OwnerSubjectId);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(" owner-123")]
    [InlineData("owner-123 ")]
    public void Create_WithInvalidOwner_ShouldFail(string subject)
    {
        Result<Property> result = CreateProperty(subject);

        Assert.True(result.IsFailure);
        Assert.Equal(PropertyErrors.InvalidOwnerSubjectId, result.Error);
    }

    [Fact]
    public void Create_WithOwnerAboveMaximum_ShouldFail()
    {
        Result<Property> result = CreateProperty(new string('a', 256));

        Assert.True(result.IsFailure);
        Assert.Equal(PropertyErrors.InvalidOwnerSubjectId, result.Error);
    }

    private static Result<Property> CreateProperty(string ownerSubjectId)
    {
        return Property.Create(
            "Rancho Costa Azul",
            "America/El_Salvador",
            PropertyTestFactory.DefaultCheckInTime,
            PropertyTestFactory.DefaultCheckOutTime,
            ownerSubjectId);
    }
}
