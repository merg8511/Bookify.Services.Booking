namespace Bookify.Services.Booking.Api.Endpoints.Administrative.RentableUnits.Seasons;

public sealed record AdminPricingSeasonResponse(
    int Id,
    DateOnly StartDate,
    DateOnly EndDate,
    decimal NightlyRate,
    string Currency,
    int Priority,
    bool IsActive);

public sealed record GetAdminPricingSeasonsResponse(
    Guid PropertyId,
    Guid UnitId,
    IReadOnlyList<AdminPricingSeasonResponse> Seasons);

public sealed record CreatePricingSeasonResponse(
    int Id);
