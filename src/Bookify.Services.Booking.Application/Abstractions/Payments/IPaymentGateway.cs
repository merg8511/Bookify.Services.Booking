using Bookify.Services.Booking.Domain.Shared;

namespace Bookify.Services.Booking.Application.Abstractions.Payments;

public interface IPaymentGateway
{
    Task<Result<PaymentGatewayResponse>>
        CreatePaymentAttemptAsync(
        CreatePaymentAttemptRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<PaymentGatewayResponse>>
        GetPaymentStatusAsync(
            string externalReference,
            CancellationToken cancellationToken = default);

    Task<Result<PaymentGatewayResponse>>
        CancelPaymentAsync(
            string externalReference,
            CancellationToken cancellationToken = default);
}
