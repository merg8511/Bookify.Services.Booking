using System.Text.Json.Serialization;

namespace Bookify.Services.Booking.Api.Endpoints.Administrative.RentableUnits.Seasons;

public sealed record UpdatePricingSeasonRequest(
    [property: JsonRequired] DateOnly StartDate,
    [property: JsonRequired] DateOnly EndDate,
    [property: JsonRequired] decimal NightlyRate,
    [property: JsonRequired] string Currency,
    [property: JsonRequired] int Priority);
