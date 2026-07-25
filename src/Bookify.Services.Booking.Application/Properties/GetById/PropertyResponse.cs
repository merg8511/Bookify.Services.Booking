namespace Bookify.Services.Booking.Application.Properties.GetById;

public sealed record PropertyResponse(
    Guid Id,
    string Name,
    string TimeZoneId,
    TimeOnly CheckInTime,
    TimeOnly CheckOutTime,
    bool IsActive);
