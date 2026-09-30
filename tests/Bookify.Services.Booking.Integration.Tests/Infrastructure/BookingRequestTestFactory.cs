using Bookify.Services.Booking.Api.Endpoints.Bookings.Create;
using Bookify.Services.Booking.Api.Endpoints.Properties.Create;

namespace Bookify.Services.Booking.Integration.Tests.Infrastructure;

internal static class BookingRequestTestFactory
{
    public static CreateBookingGuestRequest CreateGuestRequest(
        string? fullName = "John Doe",
        string? email = "john@example.com",
        string? phone = "+50377778888")
    {
        return new CreateBookingGuestRequest(fullName, email, phone);
    }

    public static CreateBookingRequest CreateBookingRequest(
        Guid propertyId = default,
        Guid rentableUnitId = default,
        DateOnly? checkInDate = null,
        DateOnly? checkOutDate = null,
        int? guestCount = 2,
        CreateBookingGuestRequest? guest = null)
    {
        DateOnly checkIn = checkInDate ?? new DateOnly(2026, 8, 10);
        DateOnly checkOut = checkOutDate ?? new DateOnly(2026, 8, 15);

        return new CreateBookingRequest(
            propertyId,
            rentableUnitId,
            checkIn,
            checkOut,
            guestCount,
            guest ?? CreateGuestRequest());
    }

    public static CreatePropertyRequest CreatePropertyRequest(
        string? name = null,
        string timeZoneId = "America/El_Salvador",
        TimeOnly? checkInTime = null,
        TimeOnly? checkOutTime = null)
    {
        return new CreatePropertyRequest(
            name ?? $"Test Property {Guid.NewGuid():N}",
            timeZoneId,
            checkInTime ?? new TimeOnly(15, 0),
            checkOutTime ?? new TimeOnly(11, 0));
    }
}
