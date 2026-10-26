namespace Bookify.Services.Booking.Application.Bookings.Administrative.ReadModels;

public sealed class AdminBookingListItemReadModel
{
    public Guid Id { get; set; }
    public string BookingReference { get; set; } = string.Empty;
    public Guid PropertyId { get; set; }
    public Guid RentableUnitId { get; set; }
    public string RentableUnitName { get; set; } = string.Empty;
    public string? CustomerSubjectId { get; set; }
    public string? GuestFullName { get; set; }
    public DateOnly CheckInDate { get; set; }
    public DateOnly CheckOutDate { get; set; }
    public int GuestCount { get; set; }
    public string Status { get; set; } = string.Empty;
    public decimal? TotalPrice { get; set; }
    public string? Currency { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
}
