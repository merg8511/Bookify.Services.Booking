namespace Bookify.Services.Booking.Application.Properties.ReadModels;

public sealed class PropertyListItemReadModel
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}
