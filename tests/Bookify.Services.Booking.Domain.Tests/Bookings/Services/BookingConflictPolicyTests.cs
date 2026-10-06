using Bookify.Services.Booking.Domain.Bookings;
using Bookify.Services.Booking.Domain.Bookings.Services;
using Bookify.Services.Booking.Domain.Bookings.ValueObjects;
using Bookify.Services.Booking.Domain.Properties;
using Bookify.Services.Booking.Domain.Tests.Infrastructure;

using DomainBooking = Bookify.Services.Booking.Domain.Bookings.Booking;

namespace Bookify.Services.Booking.Domain.Tests.Bookings.Services;

public sealed class BookingConflictPolicyTests
{
    [Theory]
    [InlineData(BookingStatus.PendingApproval)]
    [InlineData(BookingStatus.PendingPayment)]
    [InlineData(BookingStatus.Paid)]
    [InlineData(BookingStatus.Completed)]
    public void HasConflict_WhenSameUnitAndPeriodsOverlap_ShouldReturnTrue(BookingStatus status)
    {
        Guid propertyId = Guid.NewGuid();
        RentableUnit room = CreateUnit(propertyId, "Habitación A", RentableUnitType.Room);
        StayPeriod existingPeriod = CreatePeriod(10, 15);
        StayPeriod requestedPeriod = CreatePeriod(12, 18);

        DomainBooking existingBooking = BookingTestFactory.CreateBookingWithStatus(
            status,
            room,
            existingPeriod);

        bool result = BookingConflictPolicy.HasConflict(
            room,
            requestedPeriod,
            existingBooking,
            room);

        Assert.True(result);
    }

    [Fact]
    public void HasConflict_WhenExistingBookingIsCancelled_ShouldReturnFalse()
    {
        Guid propertyId = Guid.NewGuid();
        RentableUnit room = CreateUnit(propertyId, "Habitación A", RentableUnitType.Room);
        StayPeriod existingPeriod = CreatePeriod(10, 15);
        StayPeriod requestedPeriod = CreatePeriod(12, 18);

        DomainBooking existingBooking = BookingTestFactory.CreateCancelledBooking(room, existingPeriod);

        bool result = BookingConflictPolicy.HasConflict(
            room,
            requestedPeriod,
            existingBooking,
            room);

        Assert.False(result);
    }

    [Fact]
    public void HasConflict_WhenSameUnitAndPeriodsAreAdjacent_ShouldReturnFalse()
    {
        Guid propertyId = Guid.NewGuid();
        RentableUnit room = CreateUnit(propertyId, "Habitación A", RentableUnitType.Room);
        StayPeriod existingPeriod = CreatePeriod(10, 12);
        StayPeriod requestedPeriod = CreatePeriod(12, 15);

        DomainBooking existingBooking = BookingTestFactory.CreateValidBooking(room, existingPeriod);

        bool result = BookingConflictPolicy.HasConflict(
            room,
            requestedPeriod,
            existingBooking,
            room);

        Assert.False(result);
    }

    [Fact]
    public void HasConflict_WhenDifferentRoomsOverlap_ShouldReturnFalse()
    {
        Guid propertyId = Guid.NewGuid();
        RentableUnit firstRoom = CreateUnit(propertyId, "Habitación A", RentableUnitType.Room);
        RentableUnit secondRoom = CreateUnit(propertyId, "Habitación B", RentableUnitType.Room);
        StayPeriod existingPeriod = CreatePeriod(10, 15);
        StayPeriod requestedPeriod = CreatePeriod(12, 18);

        DomainBooking existingBooking = BookingTestFactory.CreateValidBooking(firstRoom, existingPeriod);

        bool result = BookingConflictPolicy.HasConflict(
            secondRoom,
            requestedPeriod,
            existingBooking,
            firstRoom);

        Assert.False(result);
    }

    [Fact]
    public void HasConflict_WhenExistingEntirePropertyAndRoomRequested_ShouldReturnTrue()
    {
        Guid propertyId = Guid.NewGuid();
        RentableUnit entireProperty = CreateUnit(propertyId, "Rancho completo", RentableUnitType.EntireProperty);
        RentableUnit room = CreateUnit(propertyId, "Habitación A", RentableUnitType.Room);
        StayPeriod existingPeriod = CreatePeriod(10, 15);
        StayPeriod requestedPeriod = CreatePeriod(12, 18);

        DomainBooking existingBooking = BookingTestFactory.CreateValidBooking(entireProperty, existingPeriod);

        bool result = BookingConflictPolicy.HasConflict(
            room,
            requestedPeriod,
            existingBooking,
            entireProperty);

        Assert.True(result);
    }

    [Fact]
    public void HasConflict_WhenExistingRoomAndEntirePropertyRequested_ShouldReturnTrue()
    {
        Guid propertyId = Guid.NewGuid();
        RentableUnit room = CreateUnit(propertyId, "Habitación A", RentableUnitType.Room);
        RentableUnit entireProperty = CreateUnit(propertyId, "Rancho completo", RentableUnitType.EntireProperty);
        StayPeriod existingPeriod = CreatePeriod(10, 15);
        StayPeriod requestedPeriod = CreatePeriod(12, 18);

        DomainBooking existingBooking = BookingTestFactory.CreateValidBooking(room, existingPeriod);

        bool result = BookingConflictPolicy.HasConflict(
            entireProperty,
            requestedPeriod,
            existingBooking,
            room);

        Assert.True(result);
    }

    [Fact]
    public void HasConflict_WhenEntirePropertyBookingsOverlap_ShouldReturnTrue()
    {
        Guid propertyId = Guid.NewGuid();
        RentableUnit entireProperty = CreateUnit(propertyId, "Rancho completo", RentableUnitType.EntireProperty);
        StayPeriod existingPeriod = CreatePeriod(10, 15);
        StayPeriod requestedPeriod = CreatePeriod(12, 18);

        DomainBooking existingBooking = BookingTestFactory.CreateValidBooking(entireProperty, existingPeriod);

        bool result = BookingConflictPolicy.HasConflict(
            entireProperty,
            requestedPeriod,
            existingBooking,
            entireProperty);

        Assert.True(result);
    }

    [Fact]
    public void HasConflict_WhenUnitsBelongToDifferentProperties_ShouldReturnFalse()
    {
        RentableUnit firstPropertyRoom = CreateUnit(Guid.NewGuid(), "Habitación propiedad A", RentableUnitType.Room);
        RentableUnit secondPropertyEntireUnit = CreateUnit(Guid.NewGuid(), "Propiedad completa B", RentableUnitType.EntireProperty);
        StayPeriod existingPeriod = CreatePeriod(10, 15);
        StayPeriod requestedPeriod = CreatePeriod(12, 18);

        DomainBooking existingBooking = BookingTestFactory.CreateValidBooking(firstPropertyRoom, existingPeriod);

        bool result = BookingConflictPolicy.HasConflict(
            secondPropertyEntireUnit,
            requestedPeriod,
            existingBooking,
            firstPropertyRoom);

        Assert.False(result);
    }

    [Fact]
    public void HasConflict_WhenExistingBookingDoesNotBelongToProvidedUnit_ShouldThrow()
    {
        Guid propertyId = Guid.NewGuid();
        RentableUnit firstRoom = CreateUnit(propertyId, "Habitación A", RentableUnitType.Room);
        RentableUnit secondRoom = CreateUnit(propertyId, "Habitación B", RentableUnitType.Room);
        StayPeriod existingPeriod = CreatePeriod(10, 15);
        StayPeriod requestedPeriod = CreatePeriod(12, 18);

        DomainBooking existingBooking = BookingTestFactory.CreateValidBooking(firstRoom, existingPeriod);

        void Action() => BookingConflictPolicy.HasConflict(
            secondRoom,
            requestedPeriod,
            existingBooking,
            secondRoom);

        Assert.Throws<InvalidOperationException>(Action);
    }

    private static RentableUnit CreateUnit(
        Guid propertyId,
        string name,
        RentableUnitType type)
    {
        return RentableUnitTestFactory.CreateValidRentableUnit(
            propertyId,
            name,
            type,
            maximumCapacity: 20,
            maxBaseGuests: 10);
    }

    private static StayPeriod CreatePeriod(int checkInDay, int checkOutDay)
    {
        return BookingTestData.CreateStayPeriod(
            checkInDay,
            checkOutDay,
            month: 7,
            year: 2026);
    }
}
