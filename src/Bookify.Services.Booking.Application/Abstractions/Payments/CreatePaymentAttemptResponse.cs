namespace Bookify.Services.Booking.Application.Abstractions.Payments;

public sealed record CreatePaymentAttemptResponse(
    string ExternalReference,
    PaymentGatewayStatus Status,
    string ClientSecret);
