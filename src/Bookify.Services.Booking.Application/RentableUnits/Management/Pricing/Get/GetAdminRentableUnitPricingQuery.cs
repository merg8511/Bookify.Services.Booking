using Bookify.Services.Booking.Application.Abstractions.Messaging;

namespace Bookify.Services.Booking.Application.RentableUnits.Management.Pricing.Get;

public sealed record GetAdminRentableUnitPricingQuery(
    Guid PropertyId,
    Guid UnitId) : IQuery<AdminRentableUnitPricingReadModel>;
