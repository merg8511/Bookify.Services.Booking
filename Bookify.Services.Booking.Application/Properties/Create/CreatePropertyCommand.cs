
using Bookify.Services.Booking.Application.Abstractions.Messaging;

namespace Bookify.Services.Booking.Application.Properties.Create;

public sealed record CreatePropertyCommand(
    string Name,
    string TimeZoneId,
    TimeOnly CheckInTime,
    TimeOnly CheckOutTime)
    : ICommand<Guid>;
