namespace Bookify.Services.Booking.Api.Endpoints.Properties.GetAvailability;

public sealed record AvailableRentableUnitResponse(
    Guid Id,
    string Name,
    string Type,
    int maximumCapacity,
    bool isEntireProperty);
