namespace Bookify.Services.Booking.Api.Endpoints.Administrative.RentableUnits;

public sealed record AdminRentableUnitResponse(
    Guid Id,
    Guid PropertyId,
    string PropertyName,
    string Name,
    string Type,
    int MaximumCapacity,
    int MaxBaseGuests,
    bool IsActive,
    bool IsEntireProperty);

public sealed record GetAdminRentableUnitsResponse(
    Guid PropertyId,
    IReadOnlyList<AdminRentableUnitResponse> Units);

public sealed record CreateRentableUnitResponse(
    Guid Id);
