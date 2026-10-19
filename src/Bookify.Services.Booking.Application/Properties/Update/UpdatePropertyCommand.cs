using Bookify.Services.Booking.Application.Abstractions.Messaging;

namespace Bookify.Services.Booking.Application.Properties.Update;

public sealed record UpdatePropertyCommand(
    Guid PropertyId,
    string Name,
    string TimeZoneId,
    TimeOnly CheckInTime,
    TimeOnly CheckOutTime) : ICommand;
