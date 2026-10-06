using Bookify.Services.Booking.Domain.Bookings;

namespace Bookify.Services.Booking.Application.Bookings.Create;

public sealed record CreateBookingResult(
    Guid Id,
    string BookingReference,
    DateTimeOffset CreatedAtUtc,
    BookingStatus Status,
    decimal AccommodationPrice,
    decimal ExtraGuestPrice,
    decimal TotalPrice,
    string Currency,
    string? GuestAccessToken = null)
{
    public override string ToString()
    {
        return $"CreateBookingResult {{ Id = {Id}, GuestAccessToken = [REDACTED] }}";
    }
}
