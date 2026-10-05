namespace Bookify.Services.Booking.Api.Security.Authorization;

using DomainBooking = Bookify.Services.Booking.Domain.Bookings.Booking;

internal sealed record BookingAuthorizationResolution(
    bool HasMetadata,
    bool HasIdentifier,
    DomainBooking? Booking)
{
    public static BookingAuthorizationResolution MissingMetadata()
    {
        return new BookingAuthorizationResolution(HasMetadata: false, HasIdentifier: false, Booking: null);
    }

    public static BookingAuthorizationResolution MissingIdentifier()
    {
        return new BookingAuthorizationResolution(HasMetadata: true, HasIdentifier: false, Booking: null);
    }

    public static BookingAuthorizationResolution Resolved(DomainBooking? booking)
    {
        return new BookingAuthorizationResolution(HasMetadata: true, HasIdentifier: true, Booking: booking);
    }
}
