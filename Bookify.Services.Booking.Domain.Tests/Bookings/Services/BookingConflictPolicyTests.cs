using DomainBooking = Bookify.Services.Booking.Domain.Bookings.Booking;
using Bookify.Services.Booking.Domain.Bookings.ValueObjects;
using Bookify.Services.Booking.Domain.Properties;
using Bookify.Services.Booking.Domain.Bookings;
using Bookify.Services.Booking.Domain.Bookings.Services;

namespace Bookify.Services.Booking.Domain.Tests.Bookings.Services
{
    public sealed class BookingConflictPolicyTests
    {
        [Theory]
        [InlineData(BookingStatus.PendingApproval)]
        [InlineData(BookingStatus.PendingPayment)]
        [InlineData(BookingStatus.Paid)]
        [InlineData(BookingStatus.Completed)]
        public void HasConflict_WhenSsameUnitAndPeriodsOverlap_ShouldReturnTrue(
            BookingStatus status)
        {
            // ARRANGE
            Guid propertyId = Guid.NewGuid();

            var room = CreateUnit(
                propertyId,
                "Habitación A",
                RentableUnitType.Room);

            var existingPeriod = CreatePeriod(
                checkInDay: 10,
                checkOutDay: 15);

            var requestedPeriod = CreatePeriod(
                checkInDay: 12,
                checkOutDay: 18);

            var existingBooking = CreateBookingWithStatus(
                room,
                existingPeriod,
                status);

            // ACT
            bool result = BookingConflictPolicy.HasConflict(
                room,
                requestedPeriod,
                existingBooking,
                room);

            // ASSERT
            Assert.True(result);
        }

        [Fact]
        public void HasConflict_WhenExistingBookingIsCancelled_ShouldReturnFalse()
        {
            // ARRANGE
            Guid propertyId = Guid.NewGuid();

            var room = CreateUnit(
                propertyId,
                "Habitación A",
                RentableUnitType.Room);

            var existingPeriod = CreatePeriod(
                checkInDay: 10,
                checkOutDay: 15);

            var requestedPeriod = CreatePeriod(
                checkInDay: 12,
                checkOutDay: 18);

            var existingBooking = DomainBooking.Create(
                room,
                existingPeriod,
                guestCount: 2)
                .Value;

            existingBooking.Reject();

            // ACT
            bool result = BookingConflictPolicy.HasConflict(
                room,
                requestedPeriod,
                existingBooking,
                room);

            // ASSERT
            Assert.False(result);
        }

        [Fact]
        public void HasConflict_WhenSameUnitAndPeriodsAreAdjacent_ShouldReturnFalse()
        {
            // ARRANGE
            Guid propertyId = Guid.NewGuid();

            var room = CreateUnit(
                propertyId,
                "Habitación A",
                RentableUnitType.Room);

            var existingPeriod = CreatePeriod(
                checkInDay: 10,
                checkOutDay: 12);

            var requestedPeriod = CreatePeriod(
                checkInDay: 12,
                checkOutDay: 15);

            var existingBooking = DomainBooking.Create(
                room,
                existingPeriod,
                guestCount: 2)
                .Value;

            // ACT
            bool result = BookingConflictPolicy.HasConflict(
                room,
                requestedPeriod,
                existingBooking,
                room);

            // ASSERT
            Assert.False(result);
        }

        [Fact]
        public void HasConflict_WhenDifferentRoomOverlap_ShouldReturnFalse()
        {
            // ARRANGE
            Guid propertyId = Guid.NewGuid();

            var firstRoom = CreateUnit(
                propertyId,
                "Habitación A",
                RentableUnitType.Room);

            var secondRoom = CreateUnit(
                propertyId,
                "Habitación B",
                RentableUnitType.Room);

            var existingPeriod = CreatePeriod(
                checkInDay: 10,
                checkOutDay: 15);

            var requestedPeriod = CreatePeriod(
                checkInDay: 12,
                checkOutDay: 18);

            var existingBooking = DomainBooking.Create(
                firstRoom,
                existingPeriod,
                guestCount: 2)
                .Value;

            // ACT
            bool result = BookingConflictPolicy.HasConflict(
                secondRoom,
                requestedPeriod,
                existingBooking,
                firstRoom);

            // ASSERT
            Assert.False(result);
        }

        [Fact]
        public void HasConflict_WhenExistingEntirePropertyAndRoomRequested_ShouldReturnTrue()
        {
            // ARRANGE
            Guid propertyId = Guid.NewGuid();

            var entireProperty = CreateUnit(
                propertyId,
                "Rancho completo",
                RentableUnitType.EntireProperty);

            var room = CreateUnit(
                propertyId,
                "Habitación A",
                RentableUnitType.Room);

            var existingPeriod = CreatePeriod(
                checkInDay: 10,
                checkOutDay: 15);

            var requestedPeriod = CreatePeriod(
                checkInDay: 12,
                checkOutDay: 18);

            var existingBooking = DomainBooking.Create(
                entireProperty,
                existingPeriod,
                guestCount: 2)
                .Value;

            // ACT
            bool result = BookingConflictPolicy.HasConflict(
                room,
                requestedPeriod,
                existingBooking,
                entireProperty);

            // ASSERT
            Assert.True(result);
        }

        [Fact]
        public void HasConflict_WhenExistingRoomAndEntirePropertyRequested_ShouldReturnTrue()
        {
            // ARRANGE
            Guid propertyId = Guid.NewGuid();

            var room = CreateUnit(
                propertyId,
                "Habitación A",
                RentableUnitType.Room);

            var entireProperty = CreateUnit(
                propertyId,
                "Rancho completo",
                RentableUnitType.EntireProperty);

            var existingPeriod = CreatePeriod(
                checkInDay: 10,
                checkOutDay: 15);

            var requestedPeriod = CreatePeriod(
                checkInDay: 12,
                checkOutDay: 18);

            var existingBooking = DomainBooking.Create(
                room,
                existingPeriod,
                guestCount: 2)
                .Value;

            // ACT
            bool result = BookingConflictPolicy.HasConflict(
                entireProperty,
                requestedPeriod,
                existingBooking,
                room);

            // ASSERT
            Assert.True(result);
        }

        [Fact]
        public void HasConflict_WhenEntirePropertyBookingsOverlap_ShouldReturnTrue()
        {
            // ARRANGE
            Guid propertyId = Guid.NewGuid();

            var entireProperty = CreateUnit(
                propertyId,
                "Rancho completo",
                RentableUnitType.EntireProperty);

            var existingPeriod = CreatePeriod(
                checkInDay: 10,
                checkOutDay: 15);

            var requestedPeriod = CreatePeriod(
                checkInDay: 12,
                checkOutDay: 18);

            var existingBooking = DomainBooking.Create(
                entireProperty,
                existingPeriod,
                guestCount: 2)
                .Value;

            // ACT
            bool result = BookingConflictPolicy.HasConflict(
                entireProperty,
                requestedPeriod,
                existingBooking,
                entireProperty);

            // ASSERT
            Assert.True(result);
        }

        [Fact]
        public void HasConflict_WhenUnitsBelongToDifferentProperties_ShouldReturnFalse()
        {
            // ARRANGE
            var firstPropertyRoom = CreateUnit(
                Guid.NewGuid(),
                "Habitación propiedad A",
                RentableUnitType.Room);

            var secondPropertyEntireUnit = CreateUnit(
                Guid.NewGuid(),
                "Propiedad completa B",
                RentableUnitType.EntireProperty);

            var existingPeriod = CreatePeriod(
                checkInDay: 10,
                checkOutDay: 15);

            var requestedPeriod = CreatePeriod(
                checkInDay: 12,
                checkOutDay: 18);

            var existingBooking = DomainBooking.Create(
                firstPropertyRoom,
                existingPeriod,
                guestCount: 2)
                .Value;

            // ACT
            bool result = BookingConflictPolicy.HasConflict(
                secondPropertyEntireUnit,
                requestedPeriod,
                existingBooking,
                firstPropertyRoom);

            // ASSERT
            Assert.False(result);
        }

        [Fact]
        public void HasConflict_WhenExistingBookingDoesNotBelongToProvidedUnit_ShouldThrow()
        {
            // ARRANGE
            Guid propertyId = Guid.NewGuid();

            var firstRoom = CreateUnit(
                propertyId,
                "Habitación A",
                RentableUnitType.Room);

            var secondRoom = CreateUnit(
                propertyId,
                "Habitación B",
                RentableUnitType.Room);

            var existingPeriod = CreatePeriod(
                checkInDay: 10,
                checkOutDay: 15);

            var requestedPeriod = CreatePeriod(
                checkInDay: 12,
                checkOutDay: 18);

            var existingBooking = DomainBooking.Create(
                firstRoom,
                existingPeriod,
                guestCount: 2)
                .Value;

            // ACT
            void Action()
            {
                BookingConflictPolicy.HasConflict(
                    secondRoom,
                    requestedPeriod,
                    existingBooking,
                    secondRoom);
            }

            // ASSERT
            Assert.Throws<InvalidOperationException>(Action);
        }

        private static RentableUnit CreateUnit(
            Guid propertyId,
            string name,
            RentableUnitType type)
        {
            return RentableUnit.Create(
                propertyId,
                name,
                type,
                maximumCapacity: 20,
                maxBaseGuests: 10)
                .Value;
        }

        private static StayPeriod CreatePeriod(
            int checkInDay,
            int checkOutDay)
        {
            return StayPeriod.Create(
                new DateOnly(2026, 7, checkInDay),
                new DateOnly(2026, 7, checkOutDay))
                .Value;
        }

        private static DomainBooking CreateBookingWithStatus(
            RentableUnit unit,
            StayPeriod period,
            BookingStatus status)
        {
            var booking = DomainBooking.Create(
                unit,
                period,
                guestCount: 2)
                .Value;

            switch (status)
            {
                case BookingStatus.PendingApproval:
                    break;

                case BookingStatus.PendingPayment:
                    booking.Approve();
                    break;

                case BookingStatus.Paid:
                    booking.Approve();
                    booking.MarkAsPaid();
                    break;

                case BookingStatus.Completed:
                    booking.Approve();
                    booking.MarkAsPaid();
                    booking.Complete();
                    break;

                case BookingStatus.Cancelled:
                    booking.Reject();
                    break;

                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(status),
                        status,
                        "Unsupported booking status.");
            }

            return booking;
        }
    }
}
