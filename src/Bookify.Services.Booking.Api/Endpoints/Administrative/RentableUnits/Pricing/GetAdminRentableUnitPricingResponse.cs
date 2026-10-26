namespace Bookify.Services.Booking.Api.Endpoints.Administrative.RentableUnits.Pricing;

public sealed record GetAdminRentableUnitPricingResponse(
    Guid PropertyId,
    Guid UnitId,
    bool IsConfigured,
    decimal? RegularNightlyRate,
    decimal? WeekendNightlyRate,
    decimal? ExtraGuestNightlyRate,
    string? Currency);
