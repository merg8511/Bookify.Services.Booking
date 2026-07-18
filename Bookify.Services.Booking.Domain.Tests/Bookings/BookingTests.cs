using DomainBooking = Bookify.Services.Booking.Domain.Bookings.Booking;
using Bookify.Services.Booking.Domain.Properties;
using Bookify.Services.Booking.Domain.Bookings.ValueObjects;
using Bookify.Services.Booking.Domain.Bookings;
using Bookify.Services.Booking.Domain.Bookings.Errors;

namespace Bookify.Services.Booking.Domain.Tests.Bookings
{
    public sealed class BookingTests
    {
        private static readonly Guid PropertyId = Guid.NewGuid();

        [Fact]
        public void Create_WithValidData_ShouldReturnPendingApprovalBooking()
        {
            // ARRANGE
            var rentableUnit = CreateRentableUnit(
                maximumCapacity: 4);

            var stayPeriod = CreateStayPeriod();

            // ACT
            var result = DomainBooking.Create(
                rentableUnit,
                stayPeriod,
                guestCount: 2);

            // ASSERT
            Assert.True(result.IsSuccess);
            Assert.NotEqual(Guid.Empty, result.Value.Id);

            Assert.Equal(
                rentableUnit.PropertyId,
                result.Value.PropertyId);

            Assert.Equal(
                rentableUnit.Id,
                result.Value.RentableUnitId);

            Assert.Equal(
                stayPeriod,
                result.Value.StayPeriod);

            Assert.Equal(
                2,
                result.Value.GuestCount);

            Assert.Equal(
                BookingStatus.PendingApproval,
                result.Value.Status);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        [InlineData(-20)]
        public void Create_WithInvalidGuestCount_ShouldReturnFailure(int invalidGuestCount)
        {
            // ARRANGE
            var rentableUnit = CreateRentableUnit();
            var stayPeriod = CreateStayPeriod();

            // ACT
            var result = DomainBooking.Create(
                rentableUnit,
                stayPeriod,
                invalidGuestCount);

            // ASSERT
            Assert.True(result.IsFailure);
            Assert.Equal(
                BookingErrors.InvalidGuestCount,
                result.Error);
        }

        [Fact]
        public void Create_WhenGuestCountExceedsCapacity_ShouldReturnFailure()
        {
            // ARRANGE
            var rentableUnit = CreateRentableUnit(maximumCapacity: 4);
            var stayPeriod = CreateStayPeriod();

            // ACT
            var result = DomainBooking.Create(
                rentableUnit,
                stayPeriod,
                guestCount: 5);

            // ASSERT
            Assert.True(result.IsFailure);
            Assert.Equal(
                BookingErrors.GuestCapacityExceeded,
                result.Error);
        }

        [Fact]
        public void Create_WhenGuestCountEqualsCapacity_ShouldReturnSuccess()
        {
            // ARRANGE
            var rentableUnit = CreateRentableUnit(
                maximumCapacity: 4);
            var stayPeriod = CreateStayPeriod();

            // ACT
            var result = DomainBooking.Create(
               rentableUnit,
               stayPeriod,
               4);

            // ASSERT
            Assert.True(result.IsSuccess);
            Assert.Equal(
                4,
                result.Value.GuestCount);
        }

        [Fact]
        public void Create_WithInactiveRentableUnit_ShouldReturnFailure()
        {
            // ARRANGE
            var rentableUnit = CreateRentableUnit();
            rentableUnit.Deactivate();

            var stayPeriod = CreateStayPeriod();

            // ACT
            var result = DomainBooking.Create(
                rentableUnit,
                stayPeriod,
                guestCount: 2);

            // ASSERT
            Assert.True(result.IsFailure);
            Assert.Equal(
                BookingErrors.RentableUnitInactive,
                result.Error);
        }

        [Fact]
        public void Create_TwiceWithSameData_ShouldCreateDifferentBookings()
        {
            // ARRANGE
            var rentableUnit = CreateRentableUnit();
            var stayPeriod = CreateStayPeriod();

            // ACT
            var firstResult = DomainBooking.Create(
                rentableUnit,
                stayPeriod,
                guestCount: 2);

            var secondResult = DomainBooking.Create(
                rentableUnit,
                stayPeriod,
                guestCount: 2);

            // ASSERT
            Assert.True(firstResult.IsSuccess);
            Assert.True(secondResult.IsSuccess);

            Assert.NotEqual(
                firstResult.Value.Id,
                secondResult.Value.Id);
        }

        [Fact]
        public void Create_WithNullRentaableUnit_ShouldThrow()
        {
            // ARRANGE
            var stayPeriod = CreateStayPeriod();

            // ACT
            void Action()
            {
                DomainBooking.Create(
                    null,
                    stayPeriod,
                    guestCount: 2);
            }

            // ASSERT
            Assert.Throws<ArgumentNullException>(Action);
        }

        [Fact]
        public void Create_WithNullStayPeriod_ShouldThrow()
        {
            // ARRANGE
            var rentableUnit = CreateRentableUnit();

            // ACT
            void Action()
            {
                DomainBooking.Create(
                    rentableUnit,
                    null,
                    guestCount: 2);
            }

            // ASSERT
            Assert.Throws<ArgumentNullException>(Action);
        }

        private static RentableUnit CreateRentableUnit(int maximumCapacity = 4)
        {
            return RentableUnit.Create(
                PropertyId,
                "Habitación principal",
                RentableUnitType.Room,
                maximumCapacity,
                maxBaseGuests: 2).Value;
        }

        private static StayPeriod CreateStayPeriod()
        {
            return StayPeriod.Create(
                new DateOnly(2026, 7, 10),
                new DateOnly(2026, 7, 12)).Value;
        }
    }
}
