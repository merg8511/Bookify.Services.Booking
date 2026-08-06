using Bookify.Services.Booking.Application.Abstractions.Messaging;
using Bookify.Services.Booking.Application.Availability.ReadModels;

namespace Bookify.Services.Booking.Application.Availability.Get;

public sealed record GetAvailabilityQuery(
    Guid PropertyId,
    DateOnly? CheckInDate,
    DateOnly? CheckOutDate,
    int? GuestCount)
    : IQuery<AvailabilityReadModel>;
