
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
        PriceBreakdown breakdown =
            PriceBreakdown.Create(
                Money.Create(accommodationPrice, currency).Value,
                Money.Create(extraGuestPrice, currency).Value)
            .Value;

        return PriceSnapshot.Create(breakdown);
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
                guestCount ?? GuestCount.Create(2).Value,
                guestDetails ?? GuestDetails.Create(
                    "John Doe",
                    "john@example.com",
                    "+50377778888").Value,
                priceSnapshot ?? CreatePriceSnapshot(),
                BookingTestTime.CreatedAtUtc,
                BookingTestTime.ApprovalDueAtUtc)
            .Value;
    }

    public static Result Approve(DomainBooking booking)
    {
        ArgumentNullException.ThrowIfNull(booking);

        return booking.Approve(
            BookingTestTime.ApprovedAtUtc,
            BookingTestTime.PaymentDueAtUtc);
    }
}
