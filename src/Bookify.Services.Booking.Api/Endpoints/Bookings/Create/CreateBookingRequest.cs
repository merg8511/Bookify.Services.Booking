namespace Bookify.Services.Booking.Api.Endpoints.Bookings.Create;

public sealed record CreateBookingRequest(
    Guid PropertyId,
    Guid RentableUnitId,
    DateOnly? CheckInDate,
    DateOnly? CheckOutDate,
    int? GuestCount);
