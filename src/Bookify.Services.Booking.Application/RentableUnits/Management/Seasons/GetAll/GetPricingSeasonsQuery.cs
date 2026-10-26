using Bookify.Services.Booking.Application.Abstractions.Messaging;

namespace Bookify.Services.Booking.Application.RentableUnits.Management.Seasons.GetAll;

public sealed record GetPricingSeasonsQuery(
    Guid PropertyId,
    Guid UnitId) : IQuery<IReadOnlyList<PricingSeasonReadModel>>;
