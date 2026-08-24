using Bookify.Services.Booking.Domain.Shared.ValueObjects;

namespace Bookify.Services.Booking.Application.Abstractions.Payments;

public sealed record CreatePaymentAttemptRequest(
    Guid BookingId,
    Money Amount,
    string IdempotencyKey);
