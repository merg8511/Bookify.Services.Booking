using Bookify.Services.Booking.Application.Abstractions.Messaging;
using Bookify.Services.Booking.Application.RentableUnits.ReadModels;

namespace Bookify.Services.Booking.Application.RentableUnits.Management.GetAll;

public sealed record GetAdminRentableUnitsQuery(
    Guid PropertyId) : IQuery<IReadOnlyList<RentableUnitDetailsReadModel>>;
