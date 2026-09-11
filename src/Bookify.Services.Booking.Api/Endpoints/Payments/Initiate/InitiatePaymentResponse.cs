namespace Bookify.Services.Booking.Api.Endpoints.Payments.Initiate;

public sealed record InitiatePaymentResponse(
    Guid PaymentId,
    Guid PaymentAttemptId,
    string Status,
    decimal Amount,
    string Currency,
    string ClientSecret);
