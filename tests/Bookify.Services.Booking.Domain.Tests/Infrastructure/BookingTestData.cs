using Bookify.Services.Booking.Domain.Bookings.Pricing;
using Bookify.Services.Booking.Domain.Bookings.ValueObjects;
using Bookify.Services.Booking.Domain.Shared.ValueObjects;

namespace Bookify.Services.Booking.Domain.Tests.Infrastructure;

internal static class BookingTestData
{
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

    public static PriceSnapshot CreatePriceSnapshot(
        decimal accommodationPrice = 400m,
        decimal extraGuestPrice = 50m,
        string currency = "USD")
    {
        PriceBreakdown breakdown = PriceBreakdown.Create(
            Money.Create(accommodationPrice, currency).Value,
            Money.Create(extraGuestPrice, currency).Value).Value;

        return PriceSnapshot.Create(breakdown);
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

    public static StayPeriod CreateStayPeriod(
        DateOnly checkInDate,
        DateOnly checkOutDate)
    {
        return StayPeriod.Create(
            checkInDate,
            checkOutDate).Value;
    }

    public static GuestCount CreateGuestCount(int count = 2)
    {
        return GuestCount.Create(count).Value;
    }
}
