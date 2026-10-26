using System.Text.Json.Serialization;

namespace Bookify.Services.Booking.Api.Endpoints.Administrative.RentableUnits;

public sealed record UpdateRentableUnitRequest(
    [property: JsonRequired] string Name,
    [property: JsonRequired] string Type,
    [property: JsonRequired] int MaximumCapacity,
    [property: JsonRequired] int MaxBaseGuests);
