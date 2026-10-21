namespace Bookify.Services.Booking.Application.RentableUnits.Management.Pricing;

public sealed class RentableUnitPricingReadModel
{
    public Guid RentableUnitId { get; set; }
    public decimal RegularNightlyRate { get; set; }
    public decimal WeekendNightlyRate { get; set; }
    public decimal ExtraGuestNightlyRate { get; set; }
    public string Currency { get; set; } = string.Empty;
}
