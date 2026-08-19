namespace Bookify.Services.Booking.Api.Endpoints.Bookings.Create;

public sealed record CreateBookingPriceResponse(
    decimal AccommodationPrice,
    decimal ExtraGuestPrice,
    decimal TotalPrice,
    string Currency);
