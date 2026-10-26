using Bookify.Services.Booking.Application.Abstractions.Messaging;
using Bookify.Services.Booking.Application.Bookings.Administrative;
using Bookify.Services.Booking.Application.Payments.Administrative.ReadModels;
using Bookify.Services.Booking.Application.Properties;
using Bookify.Services.Booking.Domain.Shared;

namespace Bookify.Services.Booking.Application.Payments.Administrative.GetByBooking;

public sealed class GetAdminBookingPaymentQueryHandler :
    IQueryHandler<GetAdminBookingPaymentQuery, AdminBookingPaymentReadModel>
{
    private readonly IPropertyReadService _propertyReadService;
    private readonly IAdminPaymentReadService _paymentReadService;

    public GetAdminBookingPaymentQueryHandler(
        IPropertyReadService propertyReadService,
        IAdminPaymentReadService paymentReadService)
    {
        _propertyReadService = propertyReadService ??
            throw new ArgumentNullException(nameof(propertyReadService));

        _paymentReadService = paymentReadService ??
            throw new ArgumentNullException(nameof(paymentReadService));
    }

    public async Task<Result<AdminBookingPaymentReadModel>> HandleAsync(
        GetAdminBookingPaymentQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var property = await _propertyReadService.GetByIdAsync(query.PropertyId, cancellationToken);

        if (property is null)
        {
            return Result<AdminBookingPaymentReadModel>.Failure(
                AdminBookingErrors.PropertyNotFound(query.PropertyId));
        }

        AdminBookingPaymentReadModel? payment = await _paymentReadService
            .GetByBookingAsync(query.PropertyId, query.BookingId, cancellationToken);

        if (payment is null)
        {
            return Result<AdminBookingPaymentReadModel>.Failure(
                AdminBookingErrors.BookingNotFound(query.BookingId));
        }

        return Result<AdminBookingPaymentReadModel>.Success(payment);
    }
}
