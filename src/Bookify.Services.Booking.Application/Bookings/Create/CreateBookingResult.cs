using Bookify.Services.Booking.Domain.Bookings;

namespace Bookify.Services.Booking.Application.Bookings.Create;

public sealed record CreateBookingResult(
    Guid Id,
    BookingStatus Status,
    decimal AccommodationPrice,
    decimal ExtraGuestPrice,
    decimal TotalPrice,
    string Currency);
