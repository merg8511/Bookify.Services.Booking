using Bookify.Services.Booking.Domain.Properties;
using Bookify.Services.Booking.Domain.Properties.Errors;
using Bookify.Services.Booking.Domain.Shared;

namespace Bookify.Services.Booking.Domain.Tests.Properties
{
    public sealed class RentableUnitTests
    {
        private static readonly Guid PropertyId = Guid.NewGuid();

        [Fact]
        public void CreateRoom_WithValidData_ShouldReturnActiveUnit()
        {
            // ACT
            var result = RentableUnit.Create(
                PropertyId,
                "Habitación principal",
                RentableUnitType.Room,
                maximumCapacity: 4,
                maxBaseGuests: 2);

            // ASSERT
            Assert.True(result.IsSuccess);
            Assert.NotEqual(Guid.Empty, result.Value.Id);
            Assert.Equal(PropertyId, result.Value.PropertyId);
            Assert.Equal("Habitación principal", result.Value.Name);
            Assert.Equal(RentableUnitType.Room, result.Value.Type);
            Assert.Equal(4, result.Value.MaximumCapacity);
            Assert.Equal(2, result.Value.MaxBaseGuests);
            Assert.True(result.Value.IsActive);
            Assert.False(result.Value.isEntireProperty);
        }

        [Fact]
        public void CreateEntireProperty_WithValidData_ShouldIdentifyEntireProperty()
        {
            // ACT
            var result = RentableUnit.Create(
                PropertyId,
                "Rancho Completo",
                RentableUnitType.EntireProperty,
                maximumCapacity: 20,
                maxBaseGuests: 10);

            // ASSERT
            Assert.True(result.IsSuccess);
            Assert.True(result.Value.isEntireProperty);
            Assert.Equal(
                RentableUnitType.EntireProperty,
                result.Value.Type);
        }

        [Fact]
        public void Create_WithEmptyPropertyId_ShouldReturnFailure()
        {
            // ACT
            var result = RentableUnit.Create(
                Guid.Empty,
                "Habitación principal",
                RentableUnitType.Room,
                maximumCapacity: 4,
                maxBaseGuests: 2);

            // ASSERT
            Assert.True(result.IsFailure);
            Assert.Equal(
                RentableUnitErrors.InvalidPropertyId,
                result.Error);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public void Create_WithInvalidName_ShouldReturnFailure(string invalidName)
        {
            // ACT
            var result = RentableUnit.Create(
                PropertyId,
                invalidName,
                RentableUnitType.Room,
                maximumCapacity: 4,
                maxBaseGuests: 2);

            // ASSERT
            Assert.True(result.IsFailure);
            Assert.Equal(
                RentableUnitErrors.InvalidName,
                result.Error);
        }

        [Fact]
        public void Create_WithUndefinedType_ShouldReturnFailure()
        {
            // ARRANGE
            var invalidType = (RentableUnitType)999;

            // ACT
            var result = RentableUnit.Create(
                PropertyId,
                "Habitación principal",
                invalidType,
                maximumCapacity: 4,
                maxBaseGuests: 2);

            // ASSERT
            Assert.True(result.IsFailure);
            Assert.Equal(
                RentableUnitErrors.InvalidType,
                result.Error);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        [InlineData(-20)]
        public void Create_WithInvalidMaximumCapacity_ShouldReturnFailure(
            int invalidCapacity)
        {
            // ACT
            var result = RentableUnit.Create(
                PropertyId,
                "Habitación principal",
                RentableUnitType.Room,
                invalidCapacity,
                maxBaseGuests: 1);

            // ASSERT
            Assert.True(result.IsFailure);
            Assert.Equal(
                RentableUnitErrors.InvalidMaximumCapacity,
                result.Error);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        [InlineData(-20)]
        public void Create_WithInvalidMaxBaseGuests_ShouldReturnFailure(
            int invalidMaxBaseGuests)
        {
            // ACT
            var result = RentableUnit.Create(
                PropertyId,
                "Habitación principal",
                RentableUnitType.Room,
                maximumCapacity: 4,
                invalidMaxBaseGuests);

            // ASSERT
            Assert.True(result.IsFailure);
            Assert.Equal(
                RentableUnitErrors.InvalidMaxBaseGuest,
                result.Error);
        }

        [Fact]
        public void Create_WhenBaseGuestsExceedCapacity_ShouldReturnFailure()
        {
            // ACT
            var result = RentableUnit.Create(
                PropertyId,
                "Habitación principal",
                RentableUnitType.Room,
                maximumCapacity: 4,
                maxBaseGuests: 5);

            // ASSERT
            Assert.True(result.IsFailure);
            Assert.Equal(
                RentableUnitErrors.BaseGuestsExceedCapacity,
                result.Error);
        }

        [Fact]
        public void Create_ShouldTrimName()
        {
            // ACT
            var result = RentableUnit.Create(
                PropertyId,
                "  Habitación principal  ",
                RentableUnitType.Room,
                maximumCapacity: 4,
                maxBaseGuests: 2);

            // ASSERT
            Assert.True(result.IsSuccess);
            Assert.Equal(
                "Habitación principal",
                result.Value.Name);
        }

        [Fact]
        public void Create_TwiceWithSameData_ShouldCreateDifferentEntities()
        {
            // ACT
            var firstResult = CreateUnitResult();
            var secondResult = CreateUnitResult();

            // ASSERT
            Assert.NotEqual(
                firstResult.Value.Id,
                secondResult.Value.Id);
        }

        [Fact]
        public void Rename_WithValidName_ShouldUpdateName()
        {
            // ARRANGE
            var unit = CreateUnit();

            // ACT
            var result = unit.Rename(
                "Suite frente al mar");

            // ASSERT
            Assert.True(result.IsSuccess);
            Assert.Equal(
                "Suite frente al mar",
                unit.Name);
        }

        [Fact]
        public void Rename_WithInvalidName_ShouldPreserveCurrentName()
        {
            // ARRANGE
            var unit = CreateUnit();
            string originalName = unit.Name;

            // ACT
            var result = unit.Rename("    ");

            // ASSERT
            Assert.True(result.IsFailure);
            Assert.Equal(
                RentableUnitErrors.InvalidName,
                result.Error);

            Assert.Equal(
                originalName,
                unit.Name);
        }

        [Fact]
        public void UpdateCapacity_WithValidValues_ShouldUpdateCapacity()
        {
            // ARRANGE
            var unit = CreateUnit();

            // ACT
            var result = unit.UpdateCapacity(
                maximumCapacity: 6,
                maxBaseGuests: 4);

            // ASSERT
            Assert.True(result.IsSuccess);
            Assert.Equal(6, unit.MaximumCapacity);
            Assert.Equal(4, unit.MaxBaseGuests);
        }

        [Fact]
        public void UpdateCapacity_WithInvalidValues_ShouldPreserveCurrentCapacity()
        {
            // ARRANGE
            var unit = CreateUnit();

            int originalMaximumCapacity = unit.MaximumCapacity;
            int originalMaxBaseGuests = unit.MaxBaseGuests;

            // ACT
            var result = unit.UpdateCapacity(
                maximumCapacity: 3,
                maxBaseGuests: 5);

            // ASSERT
            Assert.True(result.IsFailure);
            Assert.Equal(
                RentableUnitErrors.BaseGuestsExceedCapacity,
                result.Error);

            Assert.Equal(
                originalMaximumCapacity,
                unit.MaximumCapacity);

            Assert.Equal(
                originalMaxBaseGuests,
                unit.MaxBaseGuests);
        }

        [Theory]
        [InlineData(1, true)]
        [InlineData(2, true)]
        [InlineData(4, true)]
        [InlineData(5, false)]
        [InlineData(0, false)]
        [InlineData(-1, false)]
        public void CanAccommodate_ShouldEvaluateGuestCount(
            int guestCount,
            bool expectedResult)
        {
            // ARRANGE
            var unit = CreateUnit();

            // ACT
            bool result = unit.CanAccommodate(guestCount);

            // ASSERT
            Assert.Equal(expectedResult, result);
        }

        [Fact]
        public void Deactivate_WhenCalledMultipleTimes_ShouldRemainActive()
        {
            // ARRANGE
            var unit = CreateUnit();

            // ACT
            unit.Deactivate();
            unit.Deactivate();

            // ASSERT
            Assert.False(unit.IsActive);
        }

        [Fact]
        public void Activate_WhenCalledMultipleTimes_ShouldRemainActive()
        {
            // ARRANGE
            var unit = CreateUnit();

            unit.Deactivate();

            // ACT
            unit.Activate();
            unit.Activate();

            // ASSERT
            Assert.True(unit.IsActive);
        }

        [Fact]
        public void SharesInventoryWith_WhenSameUnit_ShouldReturnTrue()
        {
            // ARRANGE
            var unit = CreateUnit();

            // ACT
            bool result = unit.SharesInventoryWith(unit);

            // ASSERT
            Assert.True(result);
        }

        [Fact]
        public void SharesInventoryWith_WhenDifferentRooms_ShouldReturnFalse()
        {
            // ARRANGE
            var firtsRoom = RentableUnit.Create(
                PropertyId,
                "Habitación A",
                RentableUnitType.Room,
                maximumCapacity: 4,
                maxBaseGuests: 2).Value;

            var secondRoom = RentableUnit.Create(
                PropertyId,
                "Habitación B",
                RentableUnitType.Room,
                maximumCapacity: 4,
                maxBaseGuests: 2).Value;

            // ACT
            bool result = firtsRoom.SharesInventoryWith(secondRoom);

            // ASSERT
            Assert.False(result);
        }

        [Fact]
        public void SharesInventoryWith_WhenRoomAndEntireProperty_ShouldReturnTrue()
        {
            // ARRANGE
            var room = CreateUnit();

            var entireProperty = RentableUnit.Create(
                PropertyId,
                "Rancho Completo",
                RentableUnitType.EntireProperty,
                maximumCapacity: 20,
                maxBaseGuests: 10).Value;

            // ACT
            bool roomResult = room.SharesInventoryWith(entireProperty);
            bool entirePropertyResult = entireProperty.SharesInventoryWith(room);

            // ASSERT
            Assert.True(roomResult);
            Assert.True(entirePropertyResult);
        }

        [Fact]
        public void ShaersInventoryWith_WhenUnitsBelongToDifferentProperties_ShouldReturnFalse()
        {
            // ARRANGE
            var firsRoom = CreateUnit();

            var secondRoom = RentableUnit.Create(
                Guid.NewGuid(),
                "Habitación de otra propiedad",
                RentableUnitType.Room,
                maximumCapacity: 4,
                maxBaseGuests: 2).Value;

            // ACT
            bool result = firsRoom.SharesInventoryWith(secondRoom);

            // ASSERT
            Assert.False(result);
        }

        private static RentableUnit CreateUnit()
        {
            return CreateUnitResult().Value;
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
}
