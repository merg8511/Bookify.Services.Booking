using Bookify.Services.Booking.Application.Abstractions.Messaging;

namespace Bookify.Services.Booking.Application.Payments.Initiate;

public sealed record InitiatePaymentCommand(
    Guid BookingId,
    string IdempotencyKey)
    : ICommand<InitiatePaymentResponse>;
