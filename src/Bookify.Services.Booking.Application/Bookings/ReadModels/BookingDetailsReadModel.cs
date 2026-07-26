namespace Bookify.Services.Booking.Application;

public sealed class BookingDetailsReadModel
{
    public Guid Id { get; set; }
    public Guid PropertyId { get; set; }
    public string PropertyName { get; set; } = string.Empty;
    public Guid RentableUnitId { get; set; }
    public string RentableUnitName { get; set; } = string.Empty;
    public DateOnly CheckInDate { get; set; }
    public DateOnly CheckOutDate { get; set; }
    public int NumberOfNights { get; set; }
    public int GuestCount { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? CancellationReason { get; set; }
    public bool BlocksInventory { get; set; }
}
