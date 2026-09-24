using Bookify.Services.Booking.Application.Abstractions.Messaging;

namespace Bookify.Services.Booking.Application.Bookings.Get;

public sealed record GetBookingQuery(string Identifier) : IQuery<BookingDetailsReadModel>;
