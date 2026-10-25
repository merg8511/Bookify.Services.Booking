using Bookify.Services.Booking.Application.Abstractions.Messaging;
using Bookify.Services.Booking.Application.Payments.Administrative.ReadModels;

namespace Bookify.Services.Booking.Application.Payments.Administrative.GetByBooking;

public sealed record GetAdminBookingPaymentQuery(
    Guid PropertyId,
    Guid BookingId) : IQuery<AdminBookingPaymentReadModel>;
