namespace Bookify.Services.Booking.Application.Properties.ReadModels;

public sealed class PropertyDetailsReadModel
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string TimeZoneId { get; set; } = string.Empty;
    public TimeOnly CheckInTime { get; set; }
    public TimeOnly CheckOutTime { get; set; }
    public bool IsActive { get; set; }
}
