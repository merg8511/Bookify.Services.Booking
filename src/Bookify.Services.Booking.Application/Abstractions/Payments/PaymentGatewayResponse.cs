namespace Bookify.Services.Booking.Application.Abstractions.Payments;

public sealed record PaymentGatewayResponse(
    string ExternalReference,
    PaymentGatewayStatus Status);
