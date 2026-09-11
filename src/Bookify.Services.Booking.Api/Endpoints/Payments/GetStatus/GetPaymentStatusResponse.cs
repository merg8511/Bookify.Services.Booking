namespace Bookify.Services.Booking.Api.Endpoints.Payments.GetStatus;

public sealed record GetPaymentStatusResponse(
    Guid BookingId,
    string BookingStatus,
    string? PaymentStatus,
    string? PaymentAttemptStatus);
