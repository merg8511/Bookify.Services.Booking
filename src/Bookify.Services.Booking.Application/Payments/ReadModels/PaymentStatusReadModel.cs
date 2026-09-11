namespace Bookify.Services.Booking.Application.Payments.ReadModels;

public sealed class PaymentStatusReadModel
{
    public Guid BookingId { get; set; }
    public string BookingStatus { get; set; } = string.Empty;
    public string? PaymentStatus { get; set; }
    public string? PaymentAttemptStatus { get; set; }
}
