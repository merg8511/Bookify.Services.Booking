using Bookify.Services.Booking.Domain.Bookings.Pricing;
using Bookify.Services.Booking.Domain.Bookings.ValueObjects;
using Bookify.Services.Booking.Domain.Properties;
using Bookify.Services.Booking.Domain.Shared.ValueObjects;

using DomainBooking = Bookify.Services.Booking.Domain.Bookings.Booking;

namespace Bookify.Services.Booking.Integration.Tests.Infrastructure;

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

    public static GuestDetails CreateGuestDetails()
    {
        return GuestDetails.Create(
            "John Doe",
            "john@example.com",
            "+50377778888").Value;
    }

    public static DomainBooking CreateBooking(
        RentableUnit rentableUnit,
        StayPeriod stayPeriod,
        GuestCount? guestCount = null,
        GuestDetails? guestDetails = null,
        PriceSnapshot? priceSnapshot = null,
        DateTimeOffset? createdAtUtc = null,
        DateTimeOffset? approvalDueAtUtc = null)
    {
        DateTimeOffset creationTime =
            createdAtUtc ?? BookingTestTime.CreatedAtUtc;

        DateTimeOffset approvalDeadline =
            approvalDueAtUtc ?? creationTime.AddHours(24);

        return DomainBooking.Create(
            rentableUnit,
            stayPeriod,
            guestCount ?? GuestCount.Create(2).Value,
            guestDetails ?? CreateGuestDetails(),
            priceSnapshot ?? CreatePriceSnapshot(),
            creationTime,
            approvalDeadline).Value;
    }

    public static void Approve(
        DomainBooking booking,
        DateTimeOffset? approvedAtUtc = null,
        DateTimeOffset? paymentDueAtUtc = null)
    {
        ArgumentNullException.ThrowIfNull(booking);

        DateTimeOffset approvalTime =
            approvedAtUtc ?? BookingTestTime.ApprovedAtUtc;

        DateTimeOffset paymentDeadline =
            paymentDueAtUtc ?? approvalTime.AddMinutes(30);

        var result = booking.Approve(
            approvalTime,
            paymentDeadline);

        if (result.IsFailure)
        {
            throw new InvalidOperationException(
                $"Test setup could not approve booking: {result.Error.Code}.");
        }
    }
}
