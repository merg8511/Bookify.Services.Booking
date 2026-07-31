namespace Bookify.Services.Booking.Application.Availability.ReadModels;

public sealed class OverlappingBookingReadModel
{
    public Guid BookingId { get; set; }
    public Guid PropertyId { get; set; }
    public Guid RentableUnitId { get; set; }
    public string RentableUnitType { get; set; } = string.Empty;
    public bool IsEntireProperty { get; set; }
    public DateOnly CheckInDate { get; set; }
    public DateOnly CheckOutDate { get; set; }
    public string Status { get; set; } = string.Empty;
}
