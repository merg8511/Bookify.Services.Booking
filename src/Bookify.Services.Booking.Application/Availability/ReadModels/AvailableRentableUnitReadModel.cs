namespace Bookify.Services.Booking.Application.Availability.ReadModels;

public sealed class AvailableRentableUnitReadModel
{
    public Guid Id { get; set; }
    public Guid PropertyId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public int MaximumCapacity { get; set; }
    public int MaxBaseGuests { get; set; }
    public bool IsEntireProperty { get; set; }
    public decimal RegularNightlyRateAmount { get; set; }
    public decimal WeekendNightlyRateAmount { get; set; }
    public decimal ExtraGuestNightlyRateAmount { get; set; }
    public string PricingCurrency { get; set; } = string.Empty;
    public AvailabilityQuoteReadModel? Quote { get; set; }

}
