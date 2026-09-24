namespace Bookify.Services.Booking.Application;

public sealed class BookingDetailsReadModel
{
    public Guid Id { get; set; }
    public string BookingReference { get; set; } = string.Empty;
    public Guid PropertyId { get; set; }
    public string PropertyName { get; set; } = string.Empty;
    public Guid RentableUnitId { get; set; }
    public string RentableUnitName { get; set; } = string.Empty;
    public DateOnly CheckInDate { get; set; }
    public DateOnly CheckOutDate { get; set; }
    public int NumberOfNights { get; set; }
    public int GuestCount { get; set; }
    public string? GuestFullName { get; set; }
    public string? GuestEmail { get; set; }
    public string? GuestPhone { get; set; }
    public decimal? AccommodationPrice { get; set; }
    public decimal? ExtraGuestPrice { get; set; }
    public decimal? TotalPrice { get; set; }
    public string? Currency { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? CancellationReason { get; set; }
    public string? PaymentStatus { get; set; }
    public DateTimeOffset? CreatedAtUtc { get; set; }
    public DateTimeOffset? ApprovalDueAtUtc { get; set; }
    public DateTimeOffset? ApprovedAtUtc { get; set; }
    public DateTimeOffset? PaymentDueAtUtc { get; set; }
    public DateTimeOffset? PaidAtUtc { get; set; }
    public DateTimeOffset? CancelledAtUtc { get; set; }
    public DateTimeOffset? CompletedAtUtc { get; set; }
    public bool BlocksInventory { get; set; }
}
