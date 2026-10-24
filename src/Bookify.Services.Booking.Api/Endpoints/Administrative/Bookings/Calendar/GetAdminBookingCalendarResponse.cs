namespace Bookify.Services.Booking.Api.Endpoints.Administrative.Bookings.Calendar;

public sealed record GetAdminBookingCalendarResponse(
    Guid PropertyId,
    DateOnly RangeStart,
    DateOnly RangeEnd,
    IReadOnlyList<AdminBookingCalendarItemResponse> Items);

public sealed record AdminBookingCalendarItemResponse(
    Guid BookingId,
    Guid RentableUnitId,
    string RentableUnitName,
    DateOnly CheckInDate,
    DateOnly CheckOutDate,
    int GuestCount,
    string Status,
    bool BlocksInventory);
