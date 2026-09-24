namespace Bookify.Services.Booking.Application.Availability.ReadModels;

public sealed class AvailabilityPricingSeasonReadModel
{
    public Guid RentableUnitId { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public decimal NightlyRateAmount { get; set; }
    public string Currency { get; set; } = string.Empty;
    public int Priority { get; set; }
}
