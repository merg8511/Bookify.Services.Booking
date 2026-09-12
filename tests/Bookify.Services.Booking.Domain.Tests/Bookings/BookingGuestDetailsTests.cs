using Bookify.Services.Booking.Domain.Bookings.ValueObjects;
using Bookify.Services.Booking.Domain.Properties;
using Bookify.Services.Booking.Domain.Shared;
using Bookify.Services.Booking.Domain.Shared.ValueObjects;
using DomainBooking =
    Bookify.Services.Booking.Domain.Bookings.Booking;

namespace Bookify.Services.Booking.Domain.Tests.Bookings;

public sealed class BookingGuestDetailsTests
{
    [Fact]
    public void Create_WithGuestDetails_StoresGuestSnapshot()
    {
        // ARRANGE
        RentableUnit rentableUnit =
            CreateRentableUnit();

        StayPeriod stayPeriod =
            StayPeriod.Create(
                    new DateOnly(
                        2026,
                        10,
                        10),
                    new DateOnly(
                        2026,
                        10,
                        15))
                .Value;

        GuestCount guestCount =
            GuestCount.Create(2)
                .Value;

        GuestDetails guestDetails =
            GuestDetails.Create(
                    "Leonel Alvarenga",
                    "leonel@example.com",
                    "+50377778888")
                .Value;

        // ACT
        Result<DomainBooking> result =
            DomainBooking.Create(
                rentableUnit,
                stayPeriod,
                guestCount,
                guestDetails);

        // ASSERT
        Assert.True(result.IsSuccess);

        Assert.NotNull(
            result.Value.GuestDetails);

        Assert.Equal(
            "Leonel Alvarenga",
            result.Value
                .GuestDetails!
                .FullName);

        Assert.Equal(
            "leonel@example.com",
            result.Value
                .GuestDetails!
                .Email);

        Assert.Equal(
            "+50377778888",
            result.Value
                .GuestDetails!
                .Phone);
    }

    [Fact]
    public void Create_WithNullGuestDetails_ThrowsArgumentNullException()
    {
        // ARRANGE
        RentableUnit rentableUnit =
            CreateRentableUnit();

        StayPeriod stayPeriod =
            StayPeriod.Create(
                    new DateOnly(
                        2026,
                        10,
                        10),
                    new DateOnly(
                        2026,
                        10,
                        15))
                .Value;

        GuestCount guestCount =
            GuestCount.Create(2)
                .Value;

        // ACT
        void Action()
        {
            _ = DomainBooking.Create(
                rentableUnit,
                stayPeriod,
                guestCount,
                null!);
        }

        // ASSERT
        Assert.Throws<
            ArgumentNullException>(
                Action);
    }

    private static RentableUnit
        CreateRentableUnit()
    {
        return RentableUnit.Create(
                Guid.NewGuid(),
                "Room A",
                RentableUnitType.Room,
                maximumCapacity: 4,
                maxBaseGuests: 2)
            .Value;
    }
}
