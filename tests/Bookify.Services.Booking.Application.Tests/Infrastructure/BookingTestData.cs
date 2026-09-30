using Bookify.Services.Booking.Domain.Bookings.Pricing;
using Bookify.Services.Booking.Domain.Bookings.ValueObjects;
using Bookify.Services.Booking.Domain.Properties;
using Bookify.Services.Booking.Domain.Shared;
using Bookify.Services.Booking.Domain.Shared.ValueObjects;

using DomainBooking = Bookify.Services.Booking.Domain.Bookings.Booking;

namespace Bookify.Services.Booking.Application.Tests.Infrastructure;

internal static class BookingTestData
{
    public static PriceSnapshot CreatePriceSnapshot(
        decimal accommodationPrice = 200m,
        decimal extraGuestPrice = 0m,
        string currency = "USD")
    {
        PriceBreakdown breakdown = PriceBreakdown.Create(
            Money.Create(accommodationPrice, currency).Value,
            Money.Create(extraGuestPrice, currency).Value).Value;

        return PriceSnapshot.Create(breakdown);
    }

    public static GuestDetails CreateGuestDetails(
        string fullName = "John Doe",
        string email = "john@example.com",
        string phone = "+50377778888")
    {
        return GuestDetails.Create(
            fullName,
            email,
            phone).Value;
    }

    public static DateOnly CreateDate(int day, int month = 8, int year = 2026)
    {
        return new DateOnly(year, month, day);
    }

    public static StayPeriod CreateStayPeriod(
        int checkInDay = 10,
        int checkOutDay = 12,
        int month = 9,
        int year = 2026)
    {
        return StayPeriod.Create(
            new DateOnly(year, month, checkInDay),
            new DateOnly(year, month, checkOutDay)).Value;
    }

    public static GuestCount CreateGuestCount(int count = 2)
    {
        return GuestCount.Create(count).Value;
    }

    public static DomainBooking CreateBooking(
        RentableUnit rentableUnit,
        StayPeriod stayPeriod,
        GuestCount? guestCount = null,
        GuestDetails? guestDetails = null,
        PriceSnapshot? priceSnapshot = null)
    {
        return DomainBooking.Create(
            rentableUnit,
            stayPeriod,
            guestCount ?? CreateGuestCount(),
            guestDetails ?? CreateGuestDetails(),
            priceSnapshot ?? CreatePriceSnapshot(),
            BookingTestTime.CreatedAtUtc,
            BookingTestTime.ApprovalDueAtUtc).Value;
    }

    public static Result Approve(DomainBooking booking)
    {
        ArgumentNullException.ThrowIfNull(booking);

        return booking.Approve(
            BookingTestTime.ApprovedAtUtc,
            BookingTestTime.PaymentDueAtUtc);
    }
}
