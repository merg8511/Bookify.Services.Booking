namespace Bookify.Services.Booking.Application.RentableUnits.Management.Pricing;

public interface IRentableUnitPricingReadService
{
    Task<RentableUnitPricingReadModel?> GetByUnitIdAsync(
        Guid unitId,
        CancellationToken cancellationToken = default);

    Task<bool> HasIncompatibleSeasonCurrencyAsync(
        Guid unitId,
        string currency,
        CancellationToken cancellationToken = default);
}
