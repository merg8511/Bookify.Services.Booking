using Bookify.Services.Booking.Domain.Bookings;
using Bookify.Services.Booking.Domain.Bookings.Services;
using Bookify.Services.Booking.Domain.Bookings.ValueObjects;
using Bookify.Services.Booking.Domain.Properties;
using Bookify.Services.Booking.Domain.Shared.ValueObjects;
using Bookify.Services.Booking.Domain.Tests.Infrastructure;

using DomainBooking =
    Bookify.Services.Booking.Domain.Bookings.Booking;

namespace Bookify.Services.Booking.Domain.Tests.Bookings.Services;

public sealed class BookingConflictPolicyTests
{
    [Theory]
    [InlineData(BookingStatus.PendingApproval)]
    [InlineData(BookingStatus.PendingPayment)]
    [InlineData(BookingStatus.Paid)]
    [InlineData(BookingStatus.Completed)]
    public void HasConflict_WhenSameUnitAndPeriodsOverlap_ShouldReturnTrue(
        BookingStatus status)
    {
        Guid propertyId =
            Guid.NewGuid();

        RentableUnit room =
            CreateUnit(
                propertyId,
                "Habitación A",
                RentableUnitType.Room);

        StayPeriod existingPeriod =
            CreatePeriod(
                checkInDay: 10,
                checkOutDay: 15);

        StayPeriod requestedPeriod =
            CreatePeriod(
                checkInDay: 12,
                checkOutDay: 18);

        DomainBooking existingBooking =
            CreateBookingWithStatus(
                room,
                existingPeriod,
                status);

        bool result =
            BookingConflictPolicy.HasConflict(
                room,
                requestedPeriod,
                existingBooking,
                room);

        Assert.True(
            result);
    }

    [Fact]
    public void HasConflict_WhenExistingBookingIsCancelled_ShouldReturnFalse()
    {
        Guid propertyId =
            Guid.NewGuid();

        RentableUnit room =
            CreateUnit(
                propertyId,
                "Habitación A",
                RentableUnitType.Room);

        StayPeriod existingPeriod =
            CreatePeriod(
                checkInDay: 10,
                checkOutDay: 15);

        StayPeriod requestedPeriod =
            CreatePeriod(
                checkInDay: 12,
                checkOutDay: 18);

        DomainBooking existingBooking =
            CreateBooking(
                room,
                existingPeriod);

        Assert.True(
            existingBooking.Reject(
                    BookingTestTime.CancelledAtUtc)
                .IsSuccess);

        bool result =
            BookingConflictPolicy.HasConflict(
                room,
                requestedPeriod,
                existingBooking,
                room);

        Assert.False(
            result);
    }

    [Fact]
    public void HasConflict_WhenSameUnitAndPeriodsAreAdjacent_ShouldReturnFalse()
    {
        Guid propertyId =
            Guid.NewGuid();

        RentableUnit room =
            CreateUnit(
                propertyId,
                "Habitación A",
                RentableUnitType.Room);

        StayPeriod existingPeriod =
            CreatePeriod(
                checkInDay: 10,
                checkOutDay: 12);

        StayPeriod requestedPeriod =
            CreatePeriod(
                checkInDay: 12,
                checkOutDay: 15);

        DomainBooking existingBooking =
            CreateBooking(
                room,
                existingPeriod);

        bool result =
            BookingConflictPolicy.HasConflict(
                room,
                requestedPeriod,
                existingBooking,
                room);

        Assert.False(
            result);
    }

    [Fact]
    public void HasConflict_WhenDifferentRoomsOverlap_ShouldReturnFalse()
    {
        Guid propertyId =
            Guid.NewGuid();

        RentableUnit firstRoom =
            CreateUnit(
                propertyId,
                "Habitación A",
                RentableUnitType.Room);

        RentableUnit secondRoom =
            CreateUnit(
                propertyId,
                "Habitación B",
                RentableUnitType.Room);

        StayPeriod existingPeriod =
            CreatePeriod(
                checkInDay: 10,
                checkOutDay: 15);

        StayPeriod requestedPeriod =
            CreatePeriod(
                checkInDay: 12,
                checkOutDay: 18);

        DomainBooking existingBooking =
            CreateBooking(
                firstRoom,
                existingPeriod);

        bool result =
            BookingConflictPolicy.HasConflict(
                secondRoom,
                requestedPeriod,
                existingBooking,
                firstRoom);

        Assert.False(
            result);
    }

    [Fact]
    public void HasConflict_WhenExistingEntirePropertyAndRoomRequested_ShouldReturnTrue()
    {
        Guid propertyId =
            Guid.NewGuid();

        RentableUnit entireProperty =
            CreateUnit(
                propertyId,
                "Rancho completo",
                RentableUnitType.EntireProperty);

        RentableUnit room =
            CreateUnit(
                propertyId,
                "Habitación A",
                RentableUnitType.Room);

        StayPeriod existingPeriod =
            CreatePeriod(
                checkInDay: 10,
                checkOutDay: 15);

        StayPeriod requestedPeriod =
            CreatePeriod(
                checkInDay: 12,
                checkOutDay: 18);

        DomainBooking existingBooking =
            CreateBooking(
                entireProperty,
                existingPeriod);

        bool result =
            BookingConflictPolicy.HasConflict(
                room,
                requestedPeriod,
                existingBooking,
                entireProperty);

        Assert.True(
            result);
    }

    [Fact]
    public void HasConflict_WhenExistingRoomAndEntirePropertyRequested_ShouldReturnTrue()
    {
        Guid propertyId =
            Guid.NewGuid();

        RentableUnit room =
            CreateUnit(
                propertyId,
                "Habitación A",
                RentableUnitType.Room);

        RentableUnit entireProperty =
            CreateUnit(
                propertyId,
                "Rancho completo",
                RentableUnitType.EntireProperty);

        StayPeriod existingPeriod =
            CreatePeriod(
                checkInDay: 10,
                checkOutDay: 15);

        StayPeriod requestedPeriod =
            CreatePeriod(
                checkInDay: 12,
                checkOutDay: 18);

        DomainBooking existingBooking =
            CreateBooking(
                room,
                existingPeriod);

        bool result =
            BookingConflictPolicy.HasConflict(
                entireProperty,
                requestedPeriod,
                existingBooking,
                room);

        Assert.True(
            result);
    }

    [Fact]
    public void HasConflict_WhenEntirePropertyBookingsOverlap_ShouldReturnTrue()
    {
        Guid propertyId =
            Guid.NewGuid();

        RentableUnit entireProperty =
            CreateUnit(
                propertyId,
                "Rancho completo",
                RentableUnitType.EntireProperty);

        StayPeriod existingPeriod =
            CreatePeriod(
                checkInDay: 10,
                checkOutDay: 15);

        StayPeriod requestedPeriod =
            CreatePeriod(
                checkInDay: 12,
                checkOutDay: 18);

        DomainBooking existingBooking =
            CreateBooking(
                entireProperty,
                existingPeriod);

        bool result =
            BookingConflictPolicy.HasConflict(
                entireProperty,
                requestedPeriod,
                existingBooking,
                entireProperty);

        Assert.True(
            result);
    }

    [Fact]
    public void HasConflict_WhenUnitsBelongToDifferentProperties_ShouldReturnFalse()
    {
        RentableUnit firstPropertyRoom =
            CreateUnit(
                Guid.NewGuid(),
                "Habitación propiedad A",
                RentableUnitType.Room);

        RentableUnit secondPropertyEntireUnit =
            CreateUnit(
                Guid.NewGuid(),
                "Propiedad completa B",
                RentableUnitType.EntireProperty);

        StayPeriod existingPeriod =
            CreatePeriod(
                checkInDay: 10,
                checkOutDay: 15);

        StayPeriod requestedPeriod =
            CreatePeriod(
                checkInDay: 12,
                checkOutDay: 18);

        DomainBooking existingBooking =
            CreateBooking(
                firstPropertyRoom,
                existingPeriod);

        bool result =
            BookingConflictPolicy.HasConflict(
                secondPropertyEntireUnit,
                requestedPeriod,
                existingBooking,
                firstPropertyRoom);

        Assert.False(
            result);
    }

    [Fact]
    public void HasConflict_WhenExistingBookingDoesNotBelongToProvidedUnit_ShouldThrow()
    {
        Guid propertyId =
            Guid.NewGuid();

        RentableUnit firstRoom =
            CreateUnit(
                propertyId,
                "Habitación A",
                RentableUnitType.Room);

        RentableUnit secondRoom =
            CreateUnit(
                propertyId,
                "Habitación B",
                RentableUnitType.Room);

        StayPeriod existingPeriod =
            CreatePeriod(
                checkInDay: 10,
                checkOutDay: 15);

        StayPeriod requestedPeriod =
            CreatePeriod(
                checkInDay: 12,
                checkOutDay: 18);

        DomainBooking existingBooking =
            CreateBooking(
                firstRoom,
                existingPeriod);

        void Action()
        {
            BookingConflictPolicy.HasConflict(
                secondRoom,
                requestedPeriod,
                existingBooking,
                secondRoom);
        }

        Assert.Throws<
            InvalidOperationException>(
                Action);
    }

    private static DomainBooking CreateBookingWithStatus(
    RentableUnit unit,
    StayPeriod period,
    BookingStatus status)
    {
        DomainBooking booking =
            CreateBooking(unit, period);

        switch (status)
        {
            case BookingStatus.PendingApproval:
                break;

            case BookingStatus.PendingPayment:
                Assert.True(
                    booking.Approve(
                        BookingTestTime.ApprovedAtUtc,
                        BookingTestTime.PaymentDueAtUtc)
                    .IsSuccess);
                break;

            case BookingStatus.Paid:
                Assert.True(
                    booking.Approve(
                        BookingTestTime.ApprovedAtUtc,
                        BookingTestTime.PaymentDueAtUtc)
                    .IsSuccess);

                Assert.True(
                    booking.MarkAsPaid(
                        BookingTestTime.PaidAtUtc)
                    .IsSuccess);
                break;

            case BookingStatus.Completed:
                Assert.True(
                    booking.Approve(
                        BookingTestTime.ApprovedAtUtc,
                        BookingTestTime.PaymentDueAtUtc)
                    .IsSuccess);

                Assert.True(
                    booking.MarkAsPaid(
                        BookingTestTime.PaidAtUtc)
                    .IsSuccess);

                Assert.True(
                    booking.Complete(
                        BookingTestTime.CompletedAtUtc)
                    .IsSuccess);
                break;

            case BookingStatus.Cancelled:
                Assert.True(
                    booking.Reject(
                        BookingTestTime.CancelledAtUtc)
                    .IsSuccess);
                break;

            default:
                throw new ArgumentOutOfRangeException(
                    nameof(status),
                    status,
                    "Unsupported booking status.");
        }

        return booking;
    }

    private static DomainBooking CreateBooking(
        RentableUnit unit,
        StayPeriod period)
    {
        return DomainBooking.Create(
                unit,
                period,
                GuestCount.Create(2).Value,
                BookingTestData.CreateGuestDetails(),
                BookingTestData.CreatePriceSnapshot(),
                BookingTestTime.CreatedAtUtc,
                BookingTestTime.ApprovalDueAtUtc)
            .Value;
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
                new DateOnly(
                    2026,
                    7,
                    checkInDay),
                new DateOnly(
                    2026,
                    7,
                    checkOutDay))
            .Value;
    }
}
