using Bookify.Services.Booking.Domain.Properties;
using Bookify.Services.Booking.Domain.Properties.Errors;
using Bookify.Services.Booking.Domain.Properties.Pricing;
using Bookify.Services.Booking.Domain.Shared;
using Bookify.Services.Booking.Domain.Shared.ValueObjects;
using Bookify.Services.Booking.Domain.Tests.Infrastructure;

namespace Bookify.Services.Booking.Domain.Tests.Properties;

public sealed class RentableUnitTests
{
    private static readonly Guid PropertyId = Guid.NewGuid();

    [Fact]
    public void CreateRoom_WithValidData_ShouldReturnActiveUnit()
    {
        var result = RentableUnit.Create(
            PropertyId,
            "Habitación principal",
            RentableUnitType.Room,
            maximumCapacity: 4,
            maxBaseGuests: 2);

        Assert.True(result.IsSuccess);
        Assert.NotEqual(Guid.Empty, result.Value.Id);
        Assert.Equal(PropertyId, result.Value.PropertyId);
        Assert.Equal("Habitación principal", result.Value.Name);
        Assert.Equal(RentableUnitType.Room, result.Value.Type);
        Assert.Equal(4, result.Value.MaximumCapacity);
        Assert.Equal(2, result.Value.MaxBaseGuests);
        Assert.True(result.Value.IsActive);
        Assert.False(result.Value.IsEntireProperty);
    }

    [Fact]
    public void CreateEntireProperty_WithValidData_ShouldIdentifyEntireProperty()
    {
        var result = RentableUnit.Create(
            PropertyId,
            "Rancho Completo",
            RentableUnitType.EntireProperty,
            maximumCapacity: 20,
            maxBaseGuests: 10);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value.IsEntireProperty);
        Assert.Equal(RentableUnitType.EntireProperty, result.Value.Type);
    }

    [Fact]
    public void Create_WithEmptyPropertyId_ShouldReturnFailure()
    {
        var result = RentableUnit.Create(
            Guid.Empty,
            "Habitación principal",
            RentableUnitType.Room,
            maximumCapacity: 4,
            maxBaseGuests: 2);

        Assert.True(result.IsFailure);
        Assert.Equal(RentableUnitErrors.InvalidPropertyId, result.Error);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithInvalidName_ShouldReturnFailure(string invalidName)
    {
        var result = RentableUnit.Create(
            PropertyId,
            invalidName,
            RentableUnitType.Room,
            maximumCapacity: 4,
            maxBaseGuests: 2);

        Assert.True(result.IsFailure);
        Assert.Equal(RentableUnitErrors.InvalidName, result.Error);
    }

    [Fact]
    public void Create_WithUndefinedType_ShouldReturnFailure()
    {
        var invalidType = (RentableUnitType)999;

        var result = RentableUnit.Create(
            PropertyId,
            "Habitación principal",
            invalidType,
            maximumCapacity: 4,
            maxBaseGuests: 2);

        Assert.True(result.IsFailure);
        Assert.Equal(RentableUnitErrors.InvalidType, result.Error);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-20)]
    public void Create_WithInvalidMaximumCapacity_ShouldReturnFailure(int invalidCapacity)
    {
        var result = RentableUnit.Create(
            PropertyId,
            "Habitación principal",
            RentableUnitType.Room,
            invalidCapacity,
            maxBaseGuests: 1);

        Assert.True(result.IsFailure);
        Assert.Equal(RentableUnitErrors.InvalidMaximumCapacity, result.Error);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-20)]
    public void Create_WithInvalidMaxBaseGuests_ShouldReturnFailure(int invalidMaxBaseGuests)
    {
        var result = RentableUnit.Create(
            PropertyId,
            "Habitación principal",
            RentableUnitType.Room,
            maximumCapacity: 4,
            invalidMaxBaseGuests);

        Assert.True(result.IsFailure);
        Assert.Equal(RentableUnitErrors.InvalidMaxBaseGuests, result.Error);
    }

    [Fact]
    public void Create_WhenBaseGuestsExceedCapacity_ShouldReturnFailure()
    {
        var result = RentableUnit.Create(
            PropertyId,
            "Habitación principal",
            RentableUnitType.Room,
            maximumCapacity: 4,
            maxBaseGuests: 5);

        Assert.True(result.IsFailure);
        Assert.Equal(RentableUnitErrors.BaseGuestsExceedCapacity, result.Error);
    }

    [Fact]
    public void Create_ShouldTrimName()
    {
        var result = RentableUnit.Create(
            PropertyId,
            "  Habitación principal  ",
            RentableUnitType.Room,
            maximumCapacity: 4,
            maxBaseGuests: 2);

        Assert.True(result.IsSuccess);
        Assert.Equal("Habitación principal", result.Value.Name);
    }

    [Fact]
    public void Create_TwiceWithSameData_ShouldCreateDifferentEntities()
    {
        var firstResult = CreateUnitResult();
        var secondResult = CreateUnitResult();

        Assert.NotEqual(firstResult.Value.Id, secondResult.Value.Id);
    }

    [Fact]
    public void Rename_WithValidName_ShouldUpdateName()
    {
        var unit = CreateUnit();

        var result = unit.Rename("Suite frente al mar");

        Assert.True(result.IsSuccess);
        Assert.Equal("Suite frente al mar", unit.Name);
    }

    [Fact]
    public void Rename_WithInvalidName_ShouldPreserveCurrentName()
    {
        var unit = CreateUnit();
        string originalName = unit.Name;

        var result = unit.Rename("    ");

        Assert.True(result.IsFailure);
        Assert.Equal(RentableUnitErrors.InvalidName, result.Error);
        Assert.Equal(originalName, unit.Name);
    }

    [Fact]
    public void UpdateCapacity_WithValidValues_ShouldUpdateCapacity()
    {
        var unit = CreateUnit();

        var result = unit.UpdateCapacity(maximumCapacity: 6, maxBaseGuests: 4);

        Assert.True(result.IsSuccess);
        Assert.Equal(6, unit.MaximumCapacity);
        Assert.Equal(4, unit.MaxBaseGuests);
    }

    [Fact]
    public void UpdateCapacity_WithInvalidValues_ShouldPreserveCurrentCapacity()
    {
        var unit = CreateUnit();
        int originalMaximumCapacity = unit.MaximumCapacity;
        int originalMaxBaseGuests = unit.MaxBaseGuests;

        var result = unit.UpdateCapacity(maximumCapacity: 3, maxBaseGuests: 5);

        Assert.True(result.IsFailure);
        Assert.Equal(RentableUnitErrors.BaseGuestsExceedCapacity, result.Error);
        Assert.Equal(originalMaximumCapacity, unit.MaximumCapacity);
        Assert.Equal(originalMaxBaseGuests, unit.MaxBaseGuests);
    }

    [Theory]
    [InlineData(1, true)]
    [InlineData(2, true)]
    [InlineData(4, true)]
    [InlineData(5, false)]
    public void CanAccommodate_ShouldEvaluateGuestCount(int guestCountValue, bool expectedResult)
    {
        var unit = CreateUnit();
        var guestCount = GuestCount.Create(guestCountValue).Value;

        bool result = unit.CanAccommodate(guestCount);

        Assert.Equal(expectedResult, result);
    }

    [Fact]
    public void Deactivate_WhenCalledMultipleTimes_ShouldRemainActive()
    {
        var unit = CreateUnit();

        unit.Deactivate();
        unit.Deactivate();

        Assert.False(unit.IsActive);
    }

    [Fact]
    public void Activate_WhenCalledMultipleTimes_ShouldRemainActive()
    {
        var unit = CreateUnit();
        unit.Deactivate();

        unit.Activate();
        unit.Activate();

        Assert.True(unit.IsActive);
    }

    [Fact]
    public void SharesInventoryWith_WhenSameUnit_ShouldReturnTrue()
    {
        var unit = CreateUnit();

        bool result = unit.SharesInventoryWith(unit);

        Assert.True(result);
    }

    [Fact]
    public void SharesInventoryWith_WhenDifferentRooms_ShouldReturnFalse()
    {
        var firtsRoom = RentableUnitTestFactory.CreateValidRentableUnit(PropertyId, "Habitación A");
        var secondRoom = RentableUnitTestFactory.CreateValidRentableUnit(PropertyId, "Habitación B");

        bool result = firtsRoom.SharesInventoryWith(secondRoom);

        Assert.False(result);
    }

    [Fact]
    public void SharesInventoryWith_WhenRoomAndEntireProperty_ShouldReturnTrue()
    {
        var room = CreateUnit();
        var entireProperty = RentableUnitTestFactory.CreateEntireProperty(PropertyId);

        bool roomResult = room.SharesInventoryWith(entireProperty);
        bool entirePropertyResult = entireProperty.SharesInventoryWith(room);

        Assert.True(roomResult);
        Assert.True(entirePropertyResult);
    }

    [Fact]
    public void ShaersInventoryWith_WhenUnitsBelongToDifferentProperties_ShouldReturnFalse()
    {
        var firsRoom = CreateUnit();
        var secondRoom = RentableUnitTestFactory.CreateValidRentableUnit(
            Guid.NewGuid(),
            "Habitación de otra propiedad");

        bool result = firsRoom.SharesInventoryWith(secondRoom);

        Assert.False(result);
    }

    [Fact]
    public void Create_ShouldStartWithoutPricingConfiguration()
    {
        RentableUnit unit = CreateUnit();

        Assert.Null(unit.Pricing);
    }

    [Fact]
    public void ConfigurePricing_WithValidPricing_ShouldAssignPricing()
    {
        RentableUnit unit = CreateUnit();

        RentableUnitPricing pricing = RentableUnitPricing.Create(
            Money.Create(100m, "USD").Value,
            Money.Create(140m, "USD").Value,
            Money.Create(25m, "USD").Value).Value;

        unit.ConfigurePricing(pricing);

        Assert.Equal(pricing, unit.Pricing);
    }

    [Fact]
    public void ConfigurePricing_WhenCalledAgain_ShouldReplacePricing()
    {
        RentableUnit unit = CreateUnit();

        RentableUnitPricing initialPricing = RentableUnitPricing.Create(
            Money.Create(100m, "USD").Value,
            Money.Create(140m, "USD").Value,
            Money.Create(25m, "USD").Value).Value;

        RentableUnitPricing updatedPricing = RentableUnitPricing.Create(
            Money.Create(120m, "USD").Value,
            Money.Create(160m, "USD").Value,
            Money.Create(30m, "USD").Value).Value;

        unit.ConfigurePricing(initialPricing);
        unit.ConfigurePricing(updatedPricing);

        Assert.Equal(updatedPricing, unit.Pricing);
    }

    private static RentableUnit CreateUnit()
    {
        return RentableUnitTestFactory.CreateValidRentableUnit(
            PropertyId,
            "Habitación principal",
            RentableUnitType.Room,
            maximumCapacity: 4,
            maxBaseGuests: 2);
    }

    private static Result<RentableUnit> CreateUnitResult()
    {
        return RentableUnit.Create(
            PropertyId,
            "Habitación principal",
            RentableUnitType.Room,
            maximumCapacity: 4,
            maxBaseGuests: 2);
    }
}
