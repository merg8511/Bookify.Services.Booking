namespace Bookify.Services.Booking.Application.RentableUnits.ReadModels;

public sealed class RentableUnitListItemReadModel
{
    public Guid Id { get; set; }
    public Guid PropertyId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public int MaximumCapacity { get; set; }
    public bool IsActive { get; set; }
    public bool IsEntireProperty { get; set; }
}
