namespace Bookify.Services.Booking.Api.Endpoints.Properties.GetUnits;

public sealed record GetPropertyUnitsResponse(
    Guid PropertyId,
    IReadOnlyList<PropertyRentableUnitResponse> Units);
