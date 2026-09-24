using Bookify.Services.Booking.Application.Abstractions.Messaging;
using Bookify.Services.Booking.Application.RentableUnits.ReadModels;

namespace Bookify.Services.Booking.Application.RentableUnits.GetByProperty;

public sealed record GetPropertyUnitsQuery(Guid PropertyId) :
    IQuery<IReadOnlyList<RentableUnitListItemReadModel>>;
