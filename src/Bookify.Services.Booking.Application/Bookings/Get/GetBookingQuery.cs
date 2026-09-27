using Bookify.Services.Booking.Application.Abstractions.Messaging;
using Bookify.Services.Booking.Application.Bookings.ReadModels;

namespace Bookify.Services.Booking.Application.Bookings.Get;

public sealed record GetBookingQuery(string Identifier) : IQuery<BookingDetailsReadModel>;
