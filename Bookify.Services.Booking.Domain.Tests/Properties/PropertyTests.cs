using Bookify.Services.Booking.Domain.Properties;
using Bookify.Services.Booking.Domain.Properties.Errors;

namespace Bookify.Services.Booking.Domain.Tests.Properties
{
    public sealed class PropertyTests
    {
        private static readonly TimeOnly DefaultCheckInTime = new(15, 0);
        private static readonly TimeOnly DefaultCheckOutTime = new(11, 0);

        [Fact]
        public void Create_WithValidDate_ShouldReturnActiveProperty()
        {
            //ACT
            var result = Property.Create(
                "Rancho Costa Azul",
                "America/El_Salvador",
                DefaultCheckInTime,
                DefaultCheckOutTime);

            // ASSERT
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
        public void Create_WithInvalidName_ShouldReturnFailure(
            string invalidName)
        {
            // ACT
            var result = Property.Create(
                invalidName,
                "America/El_Salvador",
                DefaultCheckInTime,
                DefaultCheckOutTime);

            // ASSERT
            Assert.True(result.IsFailure);
            Assert.Equal(
                PropertyErrors.InvalidName,
                result.Error);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public void Create_WithInvalidTimeZoneId_ShouldReturnFailure(
            string invalidTimeZoneId)
        {
            // ACT
            var result = Property.Create(
                "Rancho Costa Azul",
                invalidTimeZoneId,
                DefaultCheckInTime,
                DefaultCheckOutTime);

            // ASSERT
            Assert.True(result.IsFailure);
            Assert.Equal(
                PropertyErrors.InvalidTimeZoneId,
                result.Error);
        }

        [Fact]
        public void Create_ShouldTrimNameAndTimeZoneId()
        {
            // ACT
            var result = Property.Create(
                "  Rancho Costa Azul  ",
                "  America/El_Salvador  ",
                DefaultCheckInTime,
                DefaultCheckOutTime);

            // ASSERT
            Assert.True(result.IsSuccess);
            Assert.Equal(
                "Rancho Costa Azul",
                result.Value.Name);

            Assert.Equal(
                "America/El_Salvador",
                result.Value.TimeZoneId);
        }

        [Fact]
        public void Create_TwiceWithSameData_ShouldCreateDiffertEntities()
        {
            // ACT
            var firstResult = Property.Create(
                "Rancho Costa Azul",
                "America/El_Salvador",
                DefaultCheckInTime,
                DefaultCheckOutTime);

            var secondResult = Property.Create(
                "Rancho Costa Azul",
                "America/El_Salvador",
                DefaultCheckInTime,
                DefaultCheckOutTime);

            // ASSERT
            Assert.True(firstResult.IsSuccess);
            Assert.True(secondResult.IsSuccess);

            Assert.NotEqual(
                firstResult.Value.Id,
                secondResult.Value.Id);
        }

        [Fact]
        public void Rename_WithValidName_ShouldUpdateName()
        {
            // ARRANGE
            var property = CreateProperty();

            // ACT
            var result = property.Rename(
                "Rancho Paraíso");

            // ASSERT
            Assert.True(result.IsSuccess);
            Assert.Equal(
                "Rancho Paraíso",
                property.Name);
        }

        [Fact]
        public void Rename_WithInvalidName_ShouldPreserveCurrentName()
        {
            // ARRANGE
            var property = CreateProperty();
            string originalName = property.Name;

            // ACT
            var result = property.Rename("         ");

            // ASSERT
            Assert.True(result.IsFailure);
            Assert.Equal(
                PropertyErrors.InvalidName,
                result.Error);

            Assert.Equal(
                originalName,
                property.Name);
        }

        [Fact]
        public void ChangeTimeZoneId_WithValidId_ShouldUpdateTimeZoneId()
        {
            // ARRANGE
            var property = CreateProperty();

            // ACT
            var result = property.ChangeTimeZoneId(
                "America/New_York");

            // ASSERT
            Assert.True(result.IsSuccess);
            Assert.Equal(
                "America/New_York",
                property.TimeZoneId);
        }

        [Fact]
        public void ChangeTimeZoneId_WithInvalidId_ShouldPreserveCurrentTimeZoneId()
        {
            // ARRANGE
            var property = CreateProperty();
            string originalTimeZoneId = property.TimeZoneId;

            // ACT
            var result = property.ChangeTimeZoneId("         ");

            // ASSERT
            Assert.True(result.IsFailure);
            Assert.Equal(
                PropertyErrors.InvalidTimeZoneId,
                result.Error);

            Assert.Equal(
                originalTimeZoneId,
                property.TimeZoneId);
        }

        [Fact]
        public void UpdateStaySchedule_ShouldUpdateCheckInAndCheckOutTimes()
        {
            // ARRANGE
            var property = CreateProperty();

            var newCheckInTime = new TimeOnly(14, 0);
            var newCheckOutTime = new TimeOnly(10, 0);

            // ACT
            property.UpdateStaySchedule(
                newCheckInTime,
                newCheckOutTime);

            // ASSERT
            Assert.Equal(
                newCheckInTime,
                property.CheckInTime);

            Assert.Equal(
                newCheckOutTime,
                property.CheckOutTime);
        }

        [Fact]
        public void UpdateStaySchedule_WithEqualTimes_ShouldBeAllowed()
        {
            // ARRANGE
            var property = CreateProperty();
            var sameTime = new TimeOnly(12, 0);

            // ACT
            property.UpdateStaySchedule(
                sameTime,
                sameTime);

            // ASSERT
            Assert.Equal(
                sameTime,
                property.CheckInTime);

            Assert.Equal(
                sameTime,
                property.CheckOutTime);
        }

        [Fact]
        public void Deactivate_WhenCalledMultipleTimes_ShouldRemainInactive()
        {
            // ARRANGE
            var property = CreateProperty();

            // ACT
            property.Deactivate();
            property.Deactivate();

            // ASSERT
            Assert.False(property.IsActive);
        }

        [Fact]
        public void Activate_WhenCalledMultipleTimes_ShouldRemainActive()
        {
            // ARRANGE
            var property = CreateProperty();
            property.Deactivate();

            // ACT
            property.Activate();
            property.Activate();

            // ASSERT
            Assert.True(property.IsActive);
        }

        private static Property CreateProperty()
        {
            return Property.Create(
                "Rancho Costa Azul",
                "America/El_Salvador",
                DefaultCheckInTime,
                DefaultCheckOutTime).Value;
        }
    }
}
