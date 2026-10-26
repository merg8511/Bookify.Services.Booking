
using Bookify.Services.Booking.Domain.Properties;
using Bookify.Services.Booking.Domain.Properties.Errors;

namespace Bookify.Services.Booking.Domain.Tests.Properties;

public sealed class RentableUnitManagementTests
{
    private static RentableUnit CreateUnit()
    {
        return RentableUnit.Create(
            Guid.NewGuid(),
            "Room A",
            RentableUnitType.Room,
            maximumCapacity: 4,
            maxBaseGuests: 2).Value;
    }

    [Fact]
    public void UpdateDetails_WithValidValues_ShouldUpdateAllFields()
    {
        // Arrange
        RentableUnit unit = CreateUnit();

        // Act
        var result = unit.UpdateDetails(
            "  Entire Property  ",
            RentableUnitType.EntireProperty,
            maximumCapacity: 10,
            maxBaseGuests: 6);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal("Entire Property", unit.Name);
        Assert.Equal(RentableUnitType.EntireProperty, unit.Type);
        Assert.Equal(10, unit.MaximumCapacity);
        Assert.Equal(6, unit.MaxBaseGuests);
        Assert.True(unit.IsEntireProperty);
    }

    [Fact]
    public void UpdateDetails_WithInvalidCapacity_ShouldPreserveAllFields()
    {
        // Arrange
        RentableUnit unit = CreateUnit();

        // Act
        var result = unit.UpdateDetails(
            "Changed Name",
            RentableUnitType.EntireProperty,
            maximumCapacity: 2,
            maxBaseGuests: 5);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(
            RentableUnitErrors.BaseGuestsExceedCapacity,
            result.Error);

        Assert.Equal("Room A", unit.Name);
        Assert.Equal(RentableUnitType.Room, unit.Type);
        Assert.Equal(4, unit.MaximumCapacity);
        Assert.Equal(2, unit.MaxBaseGuests);
    }

    [Fact]
    public void UpdateDetails_WithInvalidType_ShouldPreserveAllFields()
    {
        // Arrange
        RentableUnit unit = CreateUnit();

        // Act
        var result = unit.UpdateDetails(
            "Changed Name",
            (RentableUnitType)999,
            6,
            3);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(RentableUnitErrors.InvalidType, result.Error);

        Assert.Equal("Room A", unit.Name);
        Assert.Equal(RentableUnitType.Room, unit.Type);
        Assert.Equal(4, unit.MaximumCapacity);
        Assert.Equal(2, unit.MaxBaseGuests);
    }

    [Fact]
    public void Create_WithNameLongerThanDatabaseLimit_ShouldFail()
    {
        // Act
        var result = RentableUnit.Create(
            Guid.NewGuid(),
            new string('A', 201),
            RentableUnitType.Room,
            4,
            2);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(RentableUnitErrors.InvalidName, result.Error);
    }

    [Fact]
    public void UpdateDetails_WithInvalidName_ShouldPreserveAllFields()
    {
        // Arrange
        RentableUnit unit = CreateUnit();

        // Act
        var result = unit.UpdateDetails(
            "   ",
            RentableUnitType.EntireProperty,
            10,
            6);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(RentableUnitErrors.InvalidName, result.Error);

        Assert.Equal("Room A", unit.Name);
        Assert.Equal(RentableUnitType.Room, unit.Type);
        Assert.Equal(4, unit.MaximumCapacity);
        Assert.Equal(2, unit.MaxBaseGuests);
    }
}
