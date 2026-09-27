namespace Bookify.Services.Booking.Application.Availability.ReadModels;

public sealed record AvailableRentableUnitReadModel(
    Guid Id,
    string Name,
    string Type,
    int MaximumCapacity,
    bool IsEntireProperty,
    AvailabilityQuoteReadModel Quote);
