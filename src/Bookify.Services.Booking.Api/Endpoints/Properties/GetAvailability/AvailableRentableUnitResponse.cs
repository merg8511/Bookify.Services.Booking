namespace Bookify.Services.Booking.Api.Endpoints.Properties.GetAvailability;

public sealed record AvailableRentableUnitResponse(
    Guid Id,
    string Name,
    string Type,
    int MaximumCapacity,
    bool IsEntireProperty,
    AvailabilityQuoteResponse Quote);
