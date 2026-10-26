using Bookify.Services.Booking.Application.Abstractions.Messaging;
using Bookify.Services.Booking.Application.RentableUnits.ReadModels;

namespace Bookify.Services.Booking.Application.RentableUnits.Management.GetById;

public sealed record GetAdminRentableUnitByIdQuery(
    Guid PropertyId,
    Guid UnitId) : IQuery<RentableUnitDetailsReadModel>;
