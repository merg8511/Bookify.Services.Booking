
using Bookify.Services.Booking.Domain.Properties;
using Bookify.Services.Booking.Domain.Properties.Errors;
using Bookify.Services.Booking.Domain.Tests.Infrastructure;

namespace Bookify.Services.Booking.Domain.Tests.Properties;

public sealed class PropertyManagementTests
{
    [Fact]
    public void UpdateDetails_WithValidData_ShouldUpdateAllFields()
    {
        // Arrange
        Property property = PropertyTestFactory.CreateValidProperty();

        var checkIn = new TimeOnly(14, 0);
        var checkOut = new TimeOnly(10, 0);

        // Act
        var result = property.UpdateDetails(
            "  New Property Name  ",
            "America/New_York",
            checkIn,
            checkOut);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal("New Property Name", property.Name);
        Assert.Equal("America/New_York", property.TimeZoneId);
        Assert.Equal(checkIn, property.CheckInTime);
        Assert.Equal(checkOut, property.CheckOutTime);
    }

    [Fact]
    public void UpdateDetails_WithInvalidTimeZone_ShouldNotMutateProperty()
    {
        // Arrange
        Property property = PropertyTestFactory.CreateValidProperty();

        string originalName = property.Name;
        string originalTimeZone = property.TimeZoneId;
        TimeOnly originalCheckIn = property.CheckInTime;
        TimeOnly originalCheckOut = property.CheckOutTime;

        // Act
        var result = property.UpdateDetails(
            "Changed Property",
            "Invalid/Unknown_Zone",
            new TimeOnly(16, 0),
            new TimeOnly(9, 0));

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(PropertyErrors.InvalidTimeZoneId, result.Error);

        Assert.Equal(originalName, property.Name);
        Assert.Equal(originalTimeZone, property.TimeZoneId);
        Assert.Equal(originalCheckIn, property.CheckInTime);
        Assert.Equal(originalCheckOut, property.CheckOutTime);
    }

    [Fact]
    public void UpdateDetails_WithNameExceedingDatabaseLimit_ShouldFail()
    {
        // Arrange
        Property property = PropertyTestFactory.CreateValidProperty();

        string originalName = property.Name;

        // Act
        var result = property.UpdateDetails(
            new string('A', 201),
            "America/El_Salvador",
            new TimeOnly(15, 0),
            new TimeOnly(11, 0));

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(PropertyErrors.InvalidName, result.Error);
        Assert.Equal(originalName, property.Name);
    }
}
