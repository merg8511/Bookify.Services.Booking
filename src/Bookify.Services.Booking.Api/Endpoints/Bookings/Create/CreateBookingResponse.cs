namespace Bookify.Services.Booking.Api.Endpoints.Bookings.Create;

public sealed record CreateBookingResponse(
    Guid Id,
    string BookingReference,
    DateTimeOffset CreatedAtUtc,
    string Status,
    CreateBookingPriceResponse Price);
