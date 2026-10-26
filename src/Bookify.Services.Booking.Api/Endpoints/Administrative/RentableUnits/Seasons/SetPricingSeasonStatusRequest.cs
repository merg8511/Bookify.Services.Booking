using System.Text.Json.Serialization;

namespace Bookify.Services.Booking.Api.Endpoints.Administrative.RentableUnits.Seasons;

public sealed record SetPricingSeasonStatusRequest(
    [property: JsonRequired] bool IsActive);
