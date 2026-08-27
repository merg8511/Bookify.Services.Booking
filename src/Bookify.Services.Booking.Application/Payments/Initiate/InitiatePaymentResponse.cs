using Bookify.Services.Booking.Domain.Payments;

namespace Bookify.Services.Booking.Application.Payments.Initiate;

public sealed record InitiatePaymentResponse(
    Guid PaymentId,
    Guid PaymentAttemptId,
    string ExternalReference,
    PaymentAttemptStatus Status,
    decimal Amount,
    string Currency);
