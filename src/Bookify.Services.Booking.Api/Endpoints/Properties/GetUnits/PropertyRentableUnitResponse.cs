namespace Bookify.Services.Booking.Api.Endpoints.Properties.GetUnits;

public sealed record PropertyRentableUnitResponse(
    Guid Id,
    string Name,
    string Type,
    int MaximumCapacity,
    int MaxBaseGuests,
    bool IsEntireProperty);
