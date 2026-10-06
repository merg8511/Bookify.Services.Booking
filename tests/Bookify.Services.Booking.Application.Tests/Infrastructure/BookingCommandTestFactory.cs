using Bookify.Services.Booking.Application.Bookings.Create;
using Bookify.Services.Booking.Application.Properties.Create;

namespace Bookify.Services.Booking.Application.Tests.Infrastructure;

internal static class BookingCommandTestFactory
{
    public static readonly DateOnly DefaultCheckInDate = new(2026, 8, 10);
    public static readonly DateOnly DefaultCheckOutDate = new(2026, 8, 15);
    public static readonly TimeOnly DefaultCheckInTime = new(15, 0);
    public static readonly TimeOnly DefaultCheckOutTime = new(11, 0);

    public static CreateBookingCommand CreateBookingCommand(
        Guid? propertyId = null,
        Guid? rentableUnitId = null,
        DateOnly? checkInDate = null,
        DateOnly? checkOutDate = null,
        int? guestCount = 2,
        string? guestFullName = "John Doe",
        string? guestEmail = "john@example.com",
        string? guestPhone = "+50377778888",
        string? customerSubjectId = null)
    {
        return new CreateBookingCommand(
            propertyId ?? Guid.NewGuid(),
            rentableUnitId ?? Guid.NewGuid(),
            checkInDate ?? DefaultCheckInDate,
            checkOutDate ?? DefaultCheckOutDate,
            guestCount,
            guestFullName,
            guestEmail,
            guestPhone,
            customerSubjectId);
    }

    public static CreatePropertyCommand CreatePropertyCommand(
        string name = "Rancho Costa Azul",
        string timeZoneId = "America/El_Salvador",
        TimeOnly? checkInTime = null,
        TimeOnly? checkOutTime = null,
        string ownerSubjectId = "test-owner-subject")
    {
        return new CreatePropertyCommand(
            name,
            timeZoneId,
            checkInTime ?? DefaultCheckInTime,
            checkOutTime ?? DefaultCheckOutTime,
            ownerSubjectId);
    }
}
