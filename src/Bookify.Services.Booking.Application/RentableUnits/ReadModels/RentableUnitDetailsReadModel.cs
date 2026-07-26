namespace Bookify.Services.Booking.Application.RentableUnits.ReadModels;

public sealed class RentableUnitDetailsReadModel
{
    public Guid Id { get; set; }
    public Guid PropertyId { get; set; }
    public string PropertyName { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public int MaximumCapacity { get; set; }
    public int MaxBaseGuests { get; set; }
    public bool IsActive { get; set; }
    public bool IsEntireProperty { get; set; }
}
