using System.Text.Json.Serialization;

namespace Bookify.Services.Booking.Api.Endpoints.Administrative.RentableUnits.Pricing;

public sealed record ConfigureRentableUnitPricingRequest(
    [property: JsonRequired] decimal RegularNightlyRate,
    [property: JsonRequired] decimal WeekendNightlyRate,
    [property: JsonRequired] decimal ExtraGuestNightlyRate,
    [property: JsonRequired] string Currency);
