using Bookify.Services.Booking.Application.Abstractions.Messaging;
using Bookify.Services.Booking.Application.Bookings.ReadModels;

namespace Bookify.Services.Booking.Application.Bookings.Administrative.Calendar;

public sealed record GetAdminBookingCalendarQuery(
    Guid PropertyId,
    DateOnly? RangeStart,
    DateOnly? RangeEnd,
    Guid? UnitId,
    bool? BlocksInventory) : IQuery<IReadOnlyList<BookingCalendarItemReadModel>>;
