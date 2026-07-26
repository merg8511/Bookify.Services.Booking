namespace Bookify.Services.Booking.Application.Bookings.ReadModels;

public sealed class BookingCalendarItemReadModel
{
    public Guid BookingId { get; set; }
    public Guid RentableUnitId { get; set; }
    public string RentableUnitName { get; set; } = string.Empty;
    public DateOnly CheckInDate { get; set; }
    public DateOnly CheckOutDate { get; set; }
    public int GuestCount { get; set; }
    public string Status { get; set; } = string.Empty;
    public bool BlocksInventory { get; set; }
}
