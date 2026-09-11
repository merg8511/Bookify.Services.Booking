using Bookify.Services.Booking.Application.Abstractions.Messaging;
using Bookify.Services.Booking.Application.Payments.ReadModels;
using Bookify.Services.Booking.Domain.Shared;

namespace Bookify.Services.Booking.Application.Payments.GetStatus;

public sealed class GetPaymentStatusQueryHandler :
    IQueryHandler<GetPaymentStatusQuery, PaymentStatusReadModel>
{
    private readonly IPaymentReadService _paymentReadService;

    public GetPaymentStatusQueryHandler(IPaymentReadService paymentReadService)
    {
        _paymentReadService = paymentReadService ??
            throw new ArgumentNullException(nameof(paymentReadService));
    }

    public async Task<Result<PaymentStatusReadModel>> HandleAsync(
        GetPaymentStatusQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        PaymentStatusReadModel? paymentStatus = await _paymentReadService
            .GetStatusByBookingIdAsync(query.BookingId, cancellationToken);

        if (paymentStatus is null)
        {
            return Result<PaymentStatusReadModel>
                .Failure(GetPaymentStatusErrors.NotFound(query.BookingId));
        }

        return Result<PaymentStatusReadModel>.Success(paymentStatus);
    }
}
