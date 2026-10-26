using Bookify.Services.Booking.Application.Abstractions.Messaging;

namespace Bookify.Services.Booking.Application.RentableUnits.Management.Seasons.Create;

public sealed record CreatePricingSeasonCommand(
    Guid PropertyId,
    Guid UnitId,
    DateOnly StartDate,
    DateOnly EndDate,
    decimal NightlyRate,
    string Currency,
    int Priority) : ICommand<int>;
