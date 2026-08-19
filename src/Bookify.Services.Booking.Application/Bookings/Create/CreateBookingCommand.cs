using Bookify.Services.Booking.Application.Abstractions.Messaging;

namespace Bookify.Services.Booking.Application.Bookings.Create;

public sealed record CreateBookingCommand(
    Guid PropertyId,
    Guid RentableUnitId,
    DateOnly? CheckInDate,
    DateOnly? CheckOutDate,
    int? GuestCount) : ICommand<CreateBookingResult>;
