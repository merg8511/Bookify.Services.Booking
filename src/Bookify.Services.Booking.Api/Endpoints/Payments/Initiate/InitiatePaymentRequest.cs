namespace Bookify.Services.Booking.Api.Endpoints.Payments.Initiate;

public sealed record InitiatePaymentRequest(
    Guid BookingId);
