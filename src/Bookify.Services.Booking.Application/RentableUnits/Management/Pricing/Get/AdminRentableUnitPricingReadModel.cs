namespace Bookify.Services.Booking.Application.RentableUnits.Management.Pricing.Get;

public sealed record AdminRentableUnitPricingReadModel(
    Guid PropertyId,
    Guid UnitId,
    RentableUnitPricingReadModel? Pricing);
