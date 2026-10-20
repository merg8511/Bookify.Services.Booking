using Bookify.Services.Booking.Application.Abstractions.Messaging;

namespace Bookify.Services.Booking.Application.RentableUnits.Management.SetStatus;

public sealed record SetRentableUnitStatusCommand(
    Guid PropertyId,
    Guid UnitId,
    bool IsActive) : ICommand;
