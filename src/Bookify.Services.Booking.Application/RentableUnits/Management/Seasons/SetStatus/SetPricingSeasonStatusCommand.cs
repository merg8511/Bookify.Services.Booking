using Bookify.Services.Booking.Application.Abstractions.Messaging;

namespace Bookify.Services.Booking.Application.RentableUnits.Management.Seasons.SetStatus;

public sealed record SetPricingSeasonStatusCommand(
    Guid PropertyId,
    Guid UnitId,
    int SeasonId,
    bool IsActive) : ICommand;
