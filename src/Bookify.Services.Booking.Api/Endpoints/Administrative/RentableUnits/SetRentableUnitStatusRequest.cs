using System.Text.Json.Serialization;

namespace Bookify.Services.Booking.Api.Endpoints.Administrative.RentableUnits;

public sealed record SetRentableUnitStatusRequest(
    [property: JsonRequired] bool IsActive);
