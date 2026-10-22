namespace Bookify.Services.Booking.Application.RentableUnits.Management.Seasons;

public sealed class PricingSeasonReadModel
{
    public int Id { get; set; }
    public Guid RentableUnitId { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public decimal NightlyRate { get; set; }
    public string Currency { get; set; } = string.Empty;
    public int Priority { get; set; }
    public bool IsActive { get; set; }
}
