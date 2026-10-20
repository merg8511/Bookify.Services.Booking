using Bookify.Services.Booking.Application.Abstractions.Messaging;

namespace Bookify.Services.Booking.Application.RentableUnits.Management.Create;

public sealed record CreateRentableUnitCommand(
    Guid PropertyId,
    string Name,
    string Type,
    int MaximumCapacity,
    int MaxBaseGuests) : ICommand<Guid>;
