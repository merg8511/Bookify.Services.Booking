using Bookify.Services.Booking.Application.Abstractions.Messaging;
using Bookify.Services.Booking.Application.Payments.ReadModels;

namespace Bookify.Services.Booking.Application.Payments.GetStatus;

public sealed record GetPaymentStatusQuery(
    Guid BookingId) : IQuery<PaymentStatusReadModel>;
