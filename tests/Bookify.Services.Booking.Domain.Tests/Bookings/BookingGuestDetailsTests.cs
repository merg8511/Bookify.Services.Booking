using Bookify.Services.Booking.Domain.Bookings.ValueObjects;
using Bookify.Services.Booking.Domain.Properties;
using Bookify.Services.Booking.Domain.Shared;
using Bookify.Services.Booking.Domain.Shared.ValueObjects;
using Bookify.Services.Booking.Domain.Tests.Infrastructure;

using DomainBooking =
    Bookify.Services.Booking.Domain.Bookings.Booking;

namespace Bookify.Services.Booking.Domain.Tests.Bookings;

public sealed class BookingGuestDetailsTests
{
    [Fact]
    public void Create_WithGuestDetails_ShouldStoreGuestSnapshot()
    {
        RentableUnit rentableUnit =
            CreateRentableUnit();

        StayPeriod stayPeriod =
            CreateStayPeriod();

        GuestCount guestCount =
            GuestCount.Create(2)
                .Value;

        GuestDetails guestDetails =
            GuestDetails.Create(
                    "Leonel Alvarenga",
                    "leonel@example.com",
                    "+50377778888")
                .Value;

        Result<DomainBooking> result =
            DomainBooking.Create(
                rentableUnit,
                stayPeriod,
                guestCount,
                guestDetails,
                BookingTestData.CreatePriceSnapshot(),
                BookingTestTime.CreatedAtUtc,
                BookingTestTime.ApprovalDueAtUtc);

        Assert.True(
            result.IsSuccess);

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
    public void Create_WithNullGuestDetails_ShouldThrowArgumentNullException()
    {
        RentableUnit rentableUnit =
            CreateRentableUnit();

        StayPeriod stayPeriod =
            CreateStayPeriod();

        GuestCount guestCount =
            GuestCount.Create(2)
                .Value;

        void Action()
        {
            DomainBooking.Create(
                rentableUnit,
                stayPeriod,
                guestCount,
                null!,
                BookingTestData.CreatePriceSnapshot(),
                BookingTestTime.CreatedAtUtc,
                BookingTestTime.ApprovalDueAtUtc);
        }

        Assert.Throws<
            ArgumentNullException>(
                Action);
    }

    private static RentableUnit CreateRentableUnit()
    {
        return RentableUnit.Create(
                Guid.NewGuid(),
                "Room A",
                RentableUnitType.Room,
                maximumCapacity: 4,
                maxBaseGuests: 2)
            .Value;
    }

    private static StayPeriod CreateStayPeriod()
    {
        return StayPeriod.Create(
                new DateOnly(
                    2026,
                    10,
                    10),
                new DateOnly(
                    2026,
                    10,
                    15))
            .Value;
    }
}
