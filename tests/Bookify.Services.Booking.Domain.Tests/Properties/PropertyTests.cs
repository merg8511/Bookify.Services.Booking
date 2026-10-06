using Bookify.Services.Booking.Domain.Properties;
using Bookify.Services.Booking.Domain.Properties.Errors;
using Bookify.Services.Booking.Domain.Tests.Infrastructure;

namespace Bookify.Services.Booking.Domain.Tests.Properties;

public sealed class PropertyTests
{
    private static readonly TimeOnly DefaultCheckInTime = PropertyTestFactory.DefaultCheckInTime;
    private static readonly TimeOnly DefaultCheckOutTime = PropertyTestFactory.DefaultCheckOutTime;

    [Fact]
    public void Create_WithValidDate_ShouldReturnActiveProperty()
    {
        var result = Property.Create(
            "Rancho Costa Azul",
            "America/El_Salvador",
            DefaultCheckInTime,
            DefaultCheckOutTime,
            ownerSubjectId: "test-owner-subject");

        Assert.True(result.IsSuccess);
        Assert.NotEqual(Guid.Empty, result.Value.Id);
        Assert.Equal("Rancho Costa Azul", result.Value.Name);
        Assert.Equal("America/El_Salvador", result.Value.TimeZoneId);
        Assert.Equal(DefaultCheckInTime, result.Value.CheckInTime);
        Assert.Equal(DefaultCheckOutTime, result.Value.CheckOutTime);
        Assert.True(result.Value.IsActive);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithInvalidName_ShouldReturnFailure(string invalidName)
    {
        var result = Property.Create(
            invalidName,
            "America/El_Salvador",
            DefaultCheckInTime,
            DefaultCheckOutTime,
            ownerSubjectId: "test-owner-subject");

        Assert.True(result.IsFailure);
        Assert.Equal(PropertyErrors.InvalidName, result.Error);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithInvalidTimeZoneId_ShouldReturnFailure(string invalidTimeZoneId)
    {
        var result = Property.Create(
            "Rancho Costa Azul",
            invalidTimeZoneId,
            DefaultCheckInTime,
            DefaultCheckOutTime,
            ownerSubjectId: "test-owner-subject");

        Assert.True(result.IsFailure);
        Assert.Equal(PropertyErrors.InvalidTimeZoneId, result.Error);
    }

    [Fact]
    public void Create_ShouldTrimNameAndTimeZoneId()
    {
        var result = Property.Create(
            "  Rancho Costa Azul  ",
            "  America/El_Salvador  ",
            DefaultCheckInTime,
            DefaultCheckOutTime,
            ownerSubjectId: "test-owner-subject");

        Assert.True(result.IsSuccess);
        Assert.Equal("Rancho Costa Azul", result.Value.Name);
        Assert.Equal("America/El_Salvador", result.Value.TimeZoneId);
    }

    [Fact]
    public void Create_TwiceWithSameData_ShouldCreateDiffertEntities()
    {
        var firstResult = Property.Create(
            "Rancho Costa Azul",
            "America/El_Salvador",
            DefaultCheckInTime,
            DefaultCheckOutTime,
            ownerSubjectId: "test-owner-subject");

        var secondResult = Property.Create(
            "Rancho Costa Azul",
            "America/El_Salvador",
            DefaultCheckInTime,
            DefaultCheckOutTime,
            ownerSubjectId: "test-owner-subject");

        Assert.True(firstResult.IsSuccess);
        Assert.True(secondResult.IsSuccess);
        Assert.NotEqual(firstResult.Value.Id, secondResult.Value.Id);
    }

    [Fact]
    public void Rename_WithValidName_ShouldUpdateName()
    {
        Property property = PropertyTestFactory.CreateValidProperty();

        var result = property.Rename("Rancho Paraíso");

        Assert.True(result.IsSuccess);
        Assert.Equal("Rancho Paraíso", property.Name);
    }

    [Fact]
    public void Rename_WithInvalidName_ShouldPreserveCurrentName()
    {
        Property property = PropertyTestFactory.CreateValidProperty();
        string originalName = property.Name;

        var result = property.Rename("         ");

        Assert.True(result.IsFailure);
        Assert.Equal(PropertyErrors.InvalidName, result.Error);
        Assert.Equal(originalName, property.Name);
    }

    [Fact]
    public void ChangeTimeZoneId_WithValidId_ShouldUpdateTimeZoneId()
    {
        Property property = PropertyTestFactory.CreateValidProperty();

        var result = property.ChangeTimeZoneId("America/New_York");

        Assert.True(result.IsSuccess);
        Assert.Equal("America/New_York", property.TimeZoneId);
    }

    [Fact]
    public void ChangeTimeZoneId_WithInvalidId_ShouldPreserveCurrentTimeZoneId()
    {
        Property property = PropertyTestFactory.CreateValidProperty();
        string originalTimeZoneId = property.TimeZoneId;

        var result = property.ChangeTimeZoneId("         ");

        Assert.True(result.IsFailure);
        Assert.Equal(PropertyErrors.InvalidTimeZoneId, result.Error);
        Assert.Equal(originalTimeZoneId, property.TimeZoneId);
    }

    [Fact]
    public void UpdateStaySchedule_ShouldUpdateCheckInAndCheckOutTimes()
    {
        Property property = PropertyTestFactory.CreateValidProperty();
        var newCheckInTime = new TimeOnly(14, 0);
        var newCheckOutTime = new TimeOnly(10, 0);

        property.UpdateStaySchedule(newCheckInTime, newCheckOutTime);

        Assert.Equal(newCheckInTime, property.CheckInTime);
        Assert.Equal(newCheckOutTime, property.CheckOutTime);
    }

    [Fact]
    public void UpdateStaySchedule_WithEqualTimes_ShouldBeAllowed()
    {
        Property property = PropertyTestFactory.CreateValidProperty();
        var sameTime = new TimeOnly(12, 0);

        property.UpdateStaySchedule(sameTime, sameTime);

        Assert.Equal(sameTime, property.CheckInTime);
        Assert.Equal(sameTime, property.CheckOutTime);
    }

    [Fact]
    public void Deactivate_WhenCalledMultipleTimes_ShouldRemainInactive()
    {
        Property property = PropertyTestFactory.CreateValidProperty();

        property.Deactivate();
        property.Deactivate();

        Assert.False(property.IsActive);
    }

    [Fact]
    public void Activate_WhenCalledMultipleTimes_ShouldRemainActive()
    {
        Property property = PropertyTestFactory.CreateValidProperty();
        property.Deactivate();

        property.Activate();
        property.Activate();

        Assert.True(property.IsActive);
    }
}
