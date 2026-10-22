using Bookify.Services.Booking.Application.Abstractions.Messaging;

namespace Bookify.Services.Booking.Application.RentableUnits.Management.Seasons.GetById;

public sealed record GetPricingSeasonByIdQuery(
    Guid PropertyId,
    Guid UnitId,
    int SeasonId) : IQuery<PricingSeasonReadModel>;
