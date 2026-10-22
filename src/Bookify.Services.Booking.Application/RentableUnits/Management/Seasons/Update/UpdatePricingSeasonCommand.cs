using Bookify.Services.Booking.Application.Abstractions.Messaging;

namespace Bookify.Services.Booking.Application.RentableUnits.Management.Seasons.Update;

public sealed record UpdatePricingSeasonCommand(
    Guid PropertyId,
    Guid UnitId,
    int SeasonId,
    DateOnly StartDate,
    DateOnly EndDate,
    decimal NightlyRate,
    string Currency,
    int Priority) : ICommand;
