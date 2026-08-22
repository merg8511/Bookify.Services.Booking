namespace Bookify.Services.Booking.Application.Abstractions.Payments;

public enum PaymentGatewayStatus
{
    Pending = 1,
    Succeeded = 2,
    Failed = 3,
    Cancelled = 4
}
