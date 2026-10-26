using Bookify.Services.Booking.Application.Abstractions.Messaging;
using Bookify.Services.Booking.Application.Bookings.ReadModels;

namespace Bookify.Services.Booking.Application.Bookings.Administrative.GetById;

public sealed record GetAdminBookingByIdQuery(
    Guid PropertyId,
    Guid BookingId) : IQuery<BookingDetailsReadModel>;
