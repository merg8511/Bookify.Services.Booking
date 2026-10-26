using Bookify.Services.Booking.Application.Abstractions.Messaging;

namespace Bookify.Services.Booking.Application.RentableUnits.Management.Update;

public sealed record UpdateRentableUnitCommand(
    Guid PropertyId,
    Guid UnitId,
    string Name,
    string Type,
    int MaximumCapacity,
    int MaxBaseGuests) : ICommand;
